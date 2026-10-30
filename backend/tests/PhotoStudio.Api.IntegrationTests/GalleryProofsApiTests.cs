using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Maintenance.RunMaintenance;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the proofs of a gallery, from the web area, against a real object storage: the photographer asks for places,
/// uploads straight to the storage, registers, looks at the proofs, removes one and opens the selection, which tells the client by
/// email. Every HTTP client is a different browser.
/// </summary>
/// <param name="fixture">Shared API host.</param>
[Collection(ApiCollection.Name)]
public sealed class GalleryProofsApiTests(ApiFixture fixture)
{
    private const string SiteOrigin = "http://localhost:5173";
    private static readonly byte[] Photo = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];

    /// <summary>
    /// The whole path: places for three proofs, the upload straight to the storage, the registration, the list with addresses that
    /// really serve the photos, the removal of one (whose object disappears), the opening of the selection, and one email to the
    /// client, no matter how many times the outbox runs. After that the proofs are frozen.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheWholePath_FromTheUploadToTheEmailOfTheClient()
    {
        Assume();
        var (browser, gallery, clientEmail) = await StartAsync();

        var slots = await RequestSlotsAsync(browser, gallery.Id, ("IMG_0001.jpg", Photo.Length), ("IMG_0002.jpg", Photo.Length), ("IMG_0003.jpg", Photo.Length));
        foreach (var slot in slots)
        {
            (await PutAsync(slot, Photo)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var registered = await PostAsync(browser, $"/api/web/galleries/{gallery.Id}/proofs", new
        {
            photos = slots.Select((slot, index) => new { photoId = slot.PhotoId, name = $"IMG_000{index + 1}.jpg" }),
        });
        var body = await registered.ReadJsonAsync();
        registered.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("rejected").GetArrayLength().ShouldBe(0);
        body.GetProperty("gallery").GetProperty("photoCount").GetInt32().ShouldBe(3);
        body.GetProperty("gallery").GetProperty("allowedActions").EnumerateArray().Select(action => action.GetString()).ShouldContain("OpenSelection");

        var listed = await (await browser.GetAsync($"/api/web/galleries/{gallery.Id}/proofs", TestContext.Current.CancellationToken)).ReadJsonAsync();
        var items = listed.GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("name").GetString()).ShouldBe(["IMG_0001.jpg", "IMG_0002.jpg", "IMG_0003.jpg"]);
        using var download = await new HttpClient().GetAsync(items[0].GetProperty("url").GetString(), TestContext.Current.CancellationToken);
        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).ShouldBe(Photo);

        var removedId = items[1].GetProperty("id").GetGuid();
        var removed = await PostAsync(browser, $"/api/web/galleries/{gallery.Id}/proofs/{removedId}/remove", new { });
        removed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await removed.ReadJsonAsync()).GetProperty("photoCount").GetInt32().ShouldBe(2);
        using var gone = await new HttpClient().GetAsync(items[1].GetProperty("url").GetString(), TestContext.Current.CancellationToken);
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var opened = await PostAsync(browser, $"/api/web/galleries/{gallery.Id}/selection/open", new { });
        opened.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await opened.ReadJsonAsync()).GetProperty("status").GetString().ShouldBe("InSelection");
        await WaitForEmailAsync(clientEmail);
        await RunMaintenancePassAsync();
        await RunMaintenancePassAsync();

        var emails = fixture.Emails.Messages.Where(message => message.To == clientEmail).ToList();
        emails.Count.ShouldBe(1);
        emails[0].Subject.ShouldBe("Tus fotos de prueba ya están listas");
        emails[0].TextBody.ShouldContain("calidad baja y con marca de agua");
        emails[0].TextBody.ShouldNotContain("http");
        emails[0].HtmlBody!.ShouldContain("#3A4466");

        var frozen = await PostAsync(browser, $"/api/web/galleries/{gallery.Id}/proofs/uploads", new { files = new[] { new { name = "x.jpg", bytes = 10 } } });
        frozen.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await frozen.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe("gallery.invalid_state");
        (await PostAsync(browser, $"/api/web/galleries/{gallery.Id}/selection/open", new { })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// An object bigger than the limit, uploaded to a place that was asked for a small file, is refused when it is registered and
    /// deleted: a signed address cannot limit the size, so the API checks the real object.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AnObjectThatIsTooBig_IsRejectedAndDeleted()
    {
        Assume();
        var (browser, gallery, _) = await StartAsync();
        var slot = (await RequestSlotsAsync(browser, gallery.Id, ("small.jpg", 1000))).Single();
        (await PutAsync(slot, new byte[(int)GalleryPhoto.MaxProofBytes + 1])).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ObjectExistsAsync(gallery.Id, slot.PhotoId)).ShouldBeTrue();

        var registered = await (await PostAsync(browser, $"/api/web/galleries/{gallery.Id}/proofs", new { photos = new[] { new { photoId = slot.PhotoId, name = "small.jpg" } } })).ReadJsonAsync();

        registered.GetProperty("rejected")[0].GetProperty("reason").GetString().ShouldBe("too_big");
        registered.GetProperty("gallery").GetProperty("photoCount").GetInt32().ShouldBe(0);
        (await ObjectExistsAsync(gallery.Id, slot.PhotoId)).ShouldBeFalse();
    }

    /// <summary>
    /// The place is signed for a JPEG: an upload that declares another content type is refused by the storage itself, and registering it
    /// says it is missing. Registering something that was never uploaded says the same.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task WhatIsNotAJpeg_OrWasNeverUploaded_IsMissing()
    {
        Assume();
        var (browser, gallery, _) = await StartAsync();
        var slots = await RequestSlotsAsync(browser, gallery.Id, ("a.jpg", 100), ("b.jpg", 100));
        (await PutAsync(slots[0], Photo, "text/html")).IsSuccessStatusCode.ShouldBeFalse();

        var registered = await (await PostAsync(browser, $"/api/web/galleries/{gallery.Id}/proofs", new
        {
            photos = slots.Select(slot => new { photoId = slot.PhotoId, name = "x.jpg" }),
        })).ReadJsonAsync();

        registered.GetProperty("rejected").EnumerateArray().Select(item => item.GetProperty("reason").GetString()).ShouldBe(["missing", "missing"]);
    }

    /// <summary>
    /// Registering the same uploads twice leaves one photo of each: a request that is repeated is safe.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RegisteringTwice_RegistersOnce()
    {
        Assume();
        var (browser, gallery, _) = await StartAsync();
        var slot = (await RequestSlotsAsync(browser, gallery.Id, ("a.jpg", Photo.Length))).Single();
        await PutAsync(slot, Photo);
        var request = new { photos = new[] { new { photoId = slot.PhotoId, name = "a.jpg" } } };

        await PostAsync(browser, $"/api/web/galleries/{gallery.Id}/proofs", request);
        var again = await (await PostAsync(browser, $"/api/web/galleries/{gallery.Id}/proofs", request)).ReadJsonAsync();

        again.GetProperty("gallery").GetProperty("photoCount").GetInt32().ShouldBe(1);
        again.GetProperty("rejected").GetArrayLength().ShouldBe(0);
    }

    /// <summary>
    /// A request that is not valid is refused with a stable code: no files, too many, a file of zero bytes or over the limit, a gallery
    /// that cannot take them, and the selection opened without proofs.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task InvalidRequests_AreRefusedWithAStableCode()
    {
        Assume();
        var (browser, gallery, _) = await StartAsync();

        (await CodeOfAsync(browser, $"/api/web/galleries/{gallery.Id}/proofs/uploads", new { files = Array.Empty<object>() })).ShouldBe((HttpStatusCode.UnprocessableEntity, "gallery.invalid_photo"));
        (await CodeOfAsync(browser, $"/api/web/galleries/{gallery.Id}/proofs/uploads", new { files = Enumerable.Range(0, 51).Select(i => new { name = $"{i}.jpg", bytes = 10 }) })).ShouldBe((HttpStatusCode.UnprocessableEntity, "gallery.invalid_photo"));
        (await CodeOfAsync(browser, $"/api/web/galleries/{gallery.Id}/proofs/uploads", new { files = new[] { new { name = "a.jpg", bytes = 0 } } })).ShouldBe((HttpStatusCode.UnprocessableEntity, "gallery.invalid_photo"));
        (await CodeOfAsync(browser, $"/api/web/galleries/{gallery.Id}/proofs/uploads", new { files = new[] { new { name = "a.jpg", bytes = GalleryPhoto.MaxProofBytes + 1 } } })).ShouldBe((HttpStatusCode.UnprocessableEntity, "gallery.invalid_photo"));
        (await CodeOfAsync(browser, $"/api/web/galleries/{gallery.Id}/proofs/uploads", new { files = new[] { new { name = "", bytes = 10 } } })).ShouldBe((HttpStatusCode.UnprocessableEntity, "gallery.invalid_photo"));
        (await CodeOfAsync(browser, $"/api/web/galleries/{gallery.Id}/proofs", new { photos = Array.Empty<object>() })).ShouldBe((HttpStatusCode.UnprocessableEntity, "gallery.invalid_photo"));
        (await CodeOfAsync(browser, $"/api/web/galleries/{gallery.Id}/selection/open", new { })).ShouldBe((HttpStatusCode.Conflict, "gallery.no_photos"));
        (await CodeOfAsync(browser, $"/api/web/galleries/{gallery.Id}/proofs/{Guid.CreateVersion7()}/remove", new { })).Item1.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A gallery that is full gives no more places.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AFullGallery_GivesNoMorePlaces()
    {
        Assume();
        var (browser, gallery, _) = await StartAsync(photos: 149);

        (await CodeOfAsync(browser, $"/api/web/galleries/{gallery.Id}/proofs/uploads", new { files = new[] { new { name = "a.jpg", bytes = 10 }, new { name = "b.jpg", bytes = 10 } } }))
            .ShouldBe((HttpStatusCode.Conflict, "gallery.full"));
        (await RequestSlotsAsync(browser, gallery.Id, ("last.jpg", 10))).Count.ShouldBe(1);
    }

    /// <summary>
    /// The proofs are the owner's alone: another photographer gets 404 on every endpoint, a request without a session gets 401, the
    /// token of the app does not open them, and a request from another site is refused.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheProofs_AreTheOwnersAlone()
    {
        Assume();
        var (_, gallery, _) = await StartAsync(photos: 1);
        var stranger = await SignInAsync(await fixture.CreateUserAsync(verifiedEmail: true));
        var photoId = Guid.CreateVersion7();

        (await stranger.GetAsync($"/api/web/galleries/{gallery.Id}/proofs", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreach (var path in new[] { $"/api/web/galleries/{gallery.Id}/proofs/uploads", $"/api/web/galleries/{gallery.Id}/proofs", $"/api/web/galleries/{gallery.Id}/proofs/{photoId}/remove", $"/api/web/galleries/{gallery.Id}/selection/open" })
        {
            (await PostAsync(stranger, path, new { files = new[] { new { name = "a.jpg", bytes = 10 } }, photos = new[] { new { photoId, name = "a.jpg" } } })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        var anonymous = fixture.CreateClient();
        (await anonymous.GetAsync($"/api/web/galleries/{gallery.Id}/proofs", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await PostAsync(anonymous, $"/api/web/galleries/{gallery.Id}/selection/open", new { })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await PostAsync(stranger, $"/api/web/galleries/{gallery.Id}/selection/open", new { }, "https://evil.example.com")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A user, a booking of a client with an email, a gallery of that booking, and the browser of the user signed in.
    /// </summary>
    /// <param name="photos">Proofs the gallery already has.</param>
    /// <returns>The browser, the gallery and the email of the client.</returns>
    private async Task<(HttpClient Browser, Gallery Gallery, string ClientEmail)> StartAsync(int photos = 0)
    {
        var user = await fixture.CreateUserAsync(verifiedEmail: true);
        var clientEmail = $"client-{Guid.NewGuid():N}@example.com";
        var app = fixture.CreateClient();
        var tokens = await app.LoginAsync(user);
        var booking = await app.CreateBookingAsync(tokens.AccessToken, new Dictionary<string, object> { ["clientEmail"] = clientEmail });
        var gallery = Gallery.Restore(
            Guid.CreateVersion7(),
            0,
            user.Id,
            booking.GetProperty("id").GetGuid(),
            new GallerySnapshot("María Pérez", "Retrato Familiar", DateTimeOffset.UtcNow),
            GalleryStatus.Pending,
            30,
            5,
            Money.Create(3_000m, "CRC"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            Enumerable.Range(0, photos).Select(index => new GalleryPhoto(Guid.CreateVersion7(), $"{index}.jpg", 100, DateTimeOffset.UtcNow)));
        using var scope = fixture.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IGalleryRepository>().TryAddAsync(gallery, TestContext.Current.CancellationToken)).ShouldBeTrue();
        return (await SignInAsync(user), gallery, clientEmail);
    }

    /// <summary>
    /// Signs a user in the web area, with the code read from the email.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The browser that is signed in.</returns>
    private async Task<HttpClient> SignInAsync(TestUser user)
    {
        var browser = fixture.CreateClient();
        (await PostAsync(browser, "/api/web/auth/login", new { username = user.Username, password = user.Password })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var code = fixture.Emails.LastCodeFor($"{user.Username}@example.com");
        (await PostAsync(browser, "/api/web/auth/code/verify", new { code, rememberDevice = false })).StatusCode.ShouldBe(HttpStatusCode.OK);
        return browser;
    }

    /// <summary>
    /// Asks for the places to upload some proofs.
    /// </summary>
    /// <param name="browser">The signed-in browser.</param>
    /// <param name="galleryId">The gallery.</param>
    /// <param name="files">Name and size of each file.</param>
    /// <returns>The places, in order.</returns>
    private static async Task<List<Slot>> RequestSlotsAsync(HttpClient browser, Guid galleryId, params (string Name, int Bytes)[] files)
    {
        var response = await PostAsync(browser, $"/api/web/galleries/{galleryId}/proofs/uploads", new { files = files.Select(file => new { name = file.Name, bytes = file.Bytes }) });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        return [.. json.GetProperty("slots").EnumerateArray().Select(slot => new Slot(slot.GetProperty("photoId").GetGuid(), new Uri(slot.GetProperty("url").GetString()!), slot.GetProperty("headers").GetProperty("Content-Type").GetString()!))];
    }

    /// <summary>
    /// Uploads bytes to a place, the way the page does: straight to the storage with the headers of the signature.
    /// </summary>
    /// <param name="slot">The place.</param>
    /// <param name="content">The bytes.</param>
    /// <param name="contentType">The content type to declare; the one of the signature when omitted.</param>
    /// <returns>The answer of the storage.</returns>
    private static async Task<HttpResponseMessage> PutAsync(Slot slot, byte[] content, string? contentType = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, slot.Url) { Content = ContentOf(content, contentType ?? slot.ContentType) };
        return await new HttpClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Builds a request body with a content type.
    /// </summary>
    /// <param name="content">The bytes.</param>
    /// <param name="contentType">The content type.</param>
    /// <returns>The body.</returns>
    private static ByteArrayContent ContentOf(byte[] content, string contentType = "image/jpeg")
    {
        var body = new ByteArrayContent(content);
        body.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return body;
    }

    /// <summary>
    /// Sends a POST the way the page of the web area does: JSON, with the origin of the site.
    /// </summary>
    /// <param name="client">The browser.</param>
    /// <param name="path">Request path.</param>
    /// <param name="body">JSON body.</param>
    /// <param name="origin">The origin to send.</param>
    /// <returns>The response.</returns>
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, string origin = SiteOrigin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Origin", origin);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Sends a POST and gives the status and the stable code of the answer.
    /// </summary>
    /// <param name="client">The browser.</param>
    /// <param name="path">Request path.</param>
    /// <param name="body">JSON body.</param>
    /// <returns>The status and the code, or null when the answer has none.</returns>
    private static async Task<(HttpStatusCode, string?)> CodeOfAsync(HttpClient client, string path, object body)
    {
        var response = await PostAsync(client, path, body);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
        {
            return (response.StatusCode, null);
        }

        return (response.StatusCode, (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    /// <summary>
    /// Waits until the email to an address has been sent, running a pass of the outbox between looks: the API also delivers the
    /// outbox by itself after a change, so the message may be in the hands of that pass for a moment.
    /// </summary>
    /// <param name="address">Address of the client.</param>
    /// <returns>A task that completes when the email arrived, or after 15 seconds.</returns>
    private async Task WaitForEmailAsync(string address)
    {
        for (var attempt = 0; attempt < 30 && fixture.Emails.Messages.All(message => message.To != address); attempt++)
        {
            await RunMaintenancePassAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Runs one maintenance pass in-process, the way the worker does.
    /// </summary>
    /// <returns>A task that completes when the pass ends.</returns>
    private async Task RunMaintenancePassAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICommandHandler<RunMaintenanceCommand, MaintenanceResponse>>().HandleAsync(new RunMaintenanceCommand(), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Asks the storage whether the proof of a photo is there.
    /// </summary>
    /// <param name="galleryId">The gallery.</param>
    /// <param name="photoId">The photo.</param>
    /// <returns><see langword="true"/> when the object exists.</returns>
    private async Task<bool> ObjectExistsAsync(Guid galleryId, Guid photoId)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IObjectStorage>().GetInfoAsync(PhotoStudio.Application.Galleries.GalleryKeys.Proof(galleryId, photoId), TestContext.Current.CancellationToken) is not null;
    }

    /// <summary>
    /// Skips the test when MongoDB or the object storage is not reachable.
    /// </summary>
    private void Assume() =>
        Assert.SkipUnless(fixture.IsAvailable && fixture.StorageAvailable, "MongoDB or the object storage is not reachable; run 'docker compose up -d' in the backend folder.");

    /// <summary>
    /// A place to upload a proof, as the API gave it.
    /// </summary>
    /// <param name="PhotoId">Identifier of the photo.</param>
    /// <param name="Url">Signed address.</param>
    /// <param name="ContentType">Content type of the signature.</param>
    private sealed record Slot(Guid PhotoId, Uri Url, string ContentType);
}
