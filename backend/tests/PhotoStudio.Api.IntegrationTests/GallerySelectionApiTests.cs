using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Maintenance.RunMaintenance;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the selection of a client: the summary that needs only the link, the gallery that needs the portal session,
/// sending the photos chosen, and what the photographer gets from it (the gallery, the notice and the names of the files).
/// </summary>
/// <param name="fixture">Shared API host.</param>
[Collection(ApiCollection.Name)]
public sealed class GallerySelectionApiTests(ApiFixture fixture)
{
    private const string SiteOrigin = "http://localhost:5173";

    /// <summary>
    /// The whole path: the client sees where the gallery is with the link alone, opens it with the session, sends three photos of
    /// which one is extra, sends them again without a second effect, cannot send another selection, and the photographer gets the
    /// gallery with the counts, one notice with the numbers, and the names of the files in the order they were chosen.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheWholePath_FromTheLinkToTheNoticeOfThePhotographer()
    {
        Assume();
        var scenario = await StartAsync(GalleryStatus.InSelection, photos: 4, included: 2);

        var summary = await scenario.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}/gallery/summary", null);
        summary.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await summary.ReadJsonAsync()).GetProperty("status").GetString().ShouldBe("InSelection");
        (await scenario.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}/gallery", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var session = await scenario.OpenPortalSessionAsync(fixture);
        var opened = await (await scenario.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}/gallery", session)).ReadJsonAsync();
        opened.GetProperty("status").GetString().ShouldBe("InSelection");
        opened.GetProperty("selectablePhotos").GetInt32().ShouldBe(2);
        opened.GetProperty("extraPhotoPrice").GetProperty("amount").GetDecimal().ShouldBe(3_000m);
        opened.GetProperty("selection").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        var photos = opened.GetProperty("photos").EnumerateArray().ToList();
        photos.Count.ShouldBe(4);
        photos.ShouldAllBe(photo => photo.GetProperty("url").GetString()!.StartsWith("http", StringComparison.Ordinal));
        photos.Count(photo => photo.EnumerateObject().Any(property => property.Name == "name")).ShouldBe(0);

        var ids = photos.Select(photo => photo.GetProperty("id").GetGuid()).ToList();
        var chosen = new[] { ids[3], ids[0], ids[2] };
        var notAccepted = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/gallery/selection", session, new { photoIds = chosen });
        notAccepted.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await notAccepted.ReadCodeAsync()).ShouldBe("gallery.extras_not_accepted");

        var sent = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/gallery/selection", session, new { photoIds = chosen, acceptExtras = true });
        sent.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sentBody = await sent.ReadJsonAsync();
        sentBody.GetProperty("status").GetString().ShouldBe("InEditing");
        sentBody.GetProperty("selection").GetProperty("extraPhotos").GetInt32().ShouldBe(1);
        sentBody.GetProperty("selection").GetProperty("extraTotal").GetProperty("amount").GetDecimal().ShouldBe(3_000m);
        sentBody.GetProperty("selection").GetProperty("photoIds").EnumerateArray().Select(id => id.GetGuid()).ShouldBe(chosen);

        (await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/gallery/selection", session, new { photoIds = chosen.Reverse(), acceptExtras = true })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var another = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/gallery/selection", session, new { photoIds = new[] { ids[1] } });
        another.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await another.ReadCodeAsync()).ShouldBe("gallery.invalid_state");

        var gallery = await (await scenario.Client.SendAsync(HttpMethod.Get, $"/api/galleries/{scenario.GalleryId}", scenario.Photographer.AccessToken)).ReadJsonAsync();
        gallery.GetProperty("status").GetString().ShouldBe("InEditing");
        gallery.GetProperty("selectedPhotoCount").GetInt32().ShouldBe(3);
        gallery.GetProperty("extraPhotoCount").GetInt32().ShouldBe(1);
        gallery.GetProperty("allowedActions").EnumerateArray().Select(action => action.GetString()).ShouldBe(["UploadFinals", "ReopenSelection"]);

        var notice = await WaitForNoticeAsync(scenario);
        notice.GetProperty("selectedPhotos").GetInt32().ShouldBe(3);
        notice.GetProperty("extraPhotos").GetInt32().ShouldBe(1);
        notice.GetProperty("clientName").GetString().ShouldBe("María Pérez");
        await RunMaintenancePassAsync();
        var inbox = await (await scenario.Client.SendAsync(HttpMethod.Get, "/api/notifications", scenario.Photographer.AccessToken)).ReadJsonAsync();
        inbox.GetProperty("items").EnumerateArray().Count(item => item.GetProperty("type").GetString() == "SelectionSubmitted").ShouldBe(1);

        var browser = await SignInAsync(scenario.User);
        var names = await (await browser.GetAsync($"/api/web/galleries/{scenario.GalleryId}/selection", TestContext.Current.CancellationToken)).ReadJsonAsync();
        names.GetProperty("extraPhotos").GetInt32().ShouldBe(1);
        names.GetProperty("photos").EnumerateArray().Select(photo => photo.GetProperty("name").GetString()).ShouldBe(["IMG_0003.jpg", "IMG_0000.jpg", "IMG_0002.jpg"]);
    }

    /// <summary>
    /// What the photographer does from the app, with the token of the app and not the cookie of the web: reads the selection, reopens
    /// it with a reason (the client is told by email and chooses again, and the photographer gets a second notice), and the endpoints
    /// refuse anyone else's token and no token.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task FromTheApp_ThePhotographerReadsAndReopensTheSelection()
    {
        Assume();
        var scenario = await StartAsync(GalleryStatus.InSelection, photos: 4, included: 2);
        var session = await scenario.OpenPortalSessionAsync(fixture);
        var ids = (await (await scenario.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}/gallery", session)).ReadJsonAsync())
            .GetProperty("photos").EnumerateArray().Select(photo => photo.GetProperty("id").GetGuid()).ToList();
        (await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/gallery/selection", session, new { photoIds = new[] { ids[0], ids[1], ids[2] }, acceptExtras = true })).StatusCode.ShouldBe(HttpStatusCode.OK);
        await WaitForNoticeAsync(scenario);
        var path = $"/api/galleries/{scenario.GalleryId}";

        var selection = await (await scenario.Client.SendAsync(HttpMethod.Get, $"{path}/selection", scenario.Photographer.AccessToken)).ReadJsonAsync();
        selection.GetProperty("photos").EnumerateArray().Select(photo => photo.GetProperty("isExtra").GetBoolean()).ShouldBe([false, false, true]);
        (await scenario.Client.SendAsync(HttpMethod.Get, $"{path}/proofs", scenario.Photographer.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await scenario.Client.SendAsync(HttpMethod.Get, $"{path}/selection", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var stranger = await StartAsync(GalleryStatus.InSelection, photos: 1, included: 1);
        (await stranger.Client.SendAsync(HttpMethod.Get, $"{path}/selection", stranger.Photographer.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.Client.SendAsync(HttpMethod.Post, $"{path}/selection/reopen", stranger.Photographer.AccessToken, new { reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var noReason = await scenario.Client.SendAsync(HttpMethod.Post, $"{path}/selection/reopen", scenario.Photographer.AccessToken, new { reason = " " });
        noReason.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await noReason.ReadCodeAsync()).ShouldBe("booking.reason_required");

        var reopened = await scenario.Client.SendAsync(HttpMethod.Post, $"{path}/selection/reopen", scenario.Photographer.AccessToken, new { reason = "El cliente quiere cambiar una foto" });
        reopened.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await reopened.ReadJsonAsync();
        body.GetProperty("status").GetString().ShouldBe("InSelection");
        body.GetProperty("selectedPhotoCount").GetInt32().ShouldBe(0);
        body.GetProperty("selectionReopenReason").GetString().ShouldBe("El cliente quiere cambiar una foto");
        (await scenario.Client.SendAsync(HttpMethod.Post, $"{path}/selection/reopen", scenario.Photographer.AccessToken, new { reason = "otra vez" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var again = await (await scenario.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}/gallery", session)).ReadJsonAsync();
        again.GetProperty("status").GetString().ShouldBe("InSelection");
        again.GetProperty("selection").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        (await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/gallery/selection", session, new { photoIds = new[] { ids[3] } })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var notices = 0;
        for (var attempt = 0; attempt < 30 && notices < 2; attempt++)
        {
            await RunMaintenancePassAsync();
            var inbox = await (await scenario.Client.SendAsync(HttpMethod.Get, "/api/notifications", scenario.Photographer.AccessToken)).ReadJsonAsync();
            notices = inbox.GetProperty("items").EnumerateArray().Count(item => item.GetProperty("type").GetString() == "SelectionSubmitted");
            await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        }

        notices.ShouldBe(2);
        for (var attempt = 0; attempt < 20 && !ReopenEmailArrived(); attempt++)
        {
            await RunMaintenancePassAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        }

        ReopenEmailArrived().ShouldBeTrue();

        bool ReopenEmailArrived() => fixture.Emails.Messages.Any(message => message.To == scenario.ClientEmail && message.Subject == "Tu selección está abierta otra vez");
    }

    /// <summary>
    /// A gallery still waiting for its proofs is not there for the client, and a selection with photos that are not of the gallery or
    /// with none is refused with its own code.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task APendingGallery_IsNotThere_AndAnInvalidSelectionIsRefused()
    {
        Assume();
        var pending = await StartAsync(GalleryStatus.Pending, photos: 2, included: 2);
        (await pending.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{pending.Token}/gallery/summary", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var pendingSession = await pending.OpenPortalSessionAsync(fixture);
        (await pending.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{pending.Token}/gallery", pendingSession)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var open = await StartAsync(GalleryStatus.InSelection, photos: 2, included: 2);
        var session = await open.OpenPortalSessionAsync(fixture);
        foreach (var body in new object[] { new { photoIds = new[] { Guid.CreateVersion7() } }, new { photoIds = Array.Empty<Guid>() }, new { } })
        {
            var response = await open.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{open.Token}/gallery/selection", session, body);
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await response.ReadCodeAsync()).ShouldBe("gallery.invalid_selection");
        }

        var unchanged = await (await open.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{open.Token}/gallery", session)).ReadJsonAsync();
        unchanged.GetProperty("status").GetString().ShouldBe("InSelection");
    }

    /// <summary>
    /// The session of one booking does not open the gallery of another one, the token of the photographer does not open the portal,
    /// and the web area of another photographer does not see the selection.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheSessions_AreNotInterchangeable()
    {
        Assume();
        var mine = await StartAsync(GalleryStatus.InSelection, photos: 2, included: 1);
        var theirs = await StartAsync(GalleryStatus.InSelection, photos: 2, included: 1);
        var mySession = await mine.OpenPortalSessionAsync(fixture);
        var photoId = (await (await mine.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{mine.Token}/gallery", mySession)).ReadJsonAsync())
            .GetProperty("photos")[0].GetProperty("id").GetGuid();

        (await mine.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{theirs.Token}/gallery", mySession)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{theirs.Token}/gallery/selection", mySession, new { photoIds = new[] { photoId } })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{mine.Token}/gallery", mine.Photographer.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var stranger = await SignInAsync(theirs.User);
        (await stranger.GetAsync($"/api/web/galleries/{mine.GalleryId}/selection", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await fixture.CreateClient().GetAsync($"/api/web/galleries/{mine.GalleryId}/selection", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The finals, end to end: the photographer sees which chosen photos are package and which are extra, uploads the finals straight to
    /// the storage, cannot publish until the package is complete, publishes, and the client downloads nothing while money is owed; once
    /// the payment clears the balance the gallery is delivered by itself and the client downloads the package, the extras only after the
    /// photographer releases them.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheFinals_AreDeliveredWithAZeroBalance_AndTheExtrasOnlyWhenReleased()
    {
        Assume();
        var scenario = await StartAsync(GalleryStatus.InSelection, photos: 4, included: 2);
        var session = await scenario.OpenPortalSessionAsync(fixture);
        var ids = (await (await scenario.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}/gallery", session)).ReadJsonAsync())
            .GetProperty("photos").EnumerateArray().Select(photo => photo.GetProperty("id").GetGuid()).ToList();
        var chosen = new[] { ids[2], ids[0], ids[3] };
        (await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/gallery/selection", session, new { photoIds = chosen, acceptExtras = true })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var browser = await SignInAsync(scenario.User);
        var selection = await (await browser.GetAsync($"/api/web/galleries/{scenario.GalleryId}/selection", TestContext.Current.CancellationToken)).ReadJsonAsync();
        selection.GetProperty("photos").EnumerateArray().Select(photo => photo.GetProperty("isExtra").GetBoolean()).ShouldBe([false, false, true]);

        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1 };
        var path = $"/api/web/galleries/{scenario.GalleryId}";
        await UploadFinalsAsync(browser, path, [chosen[0]], bytes);
        var early = await PostAsync(browser, $"{path}/finals/publish", new { });
        early.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await early.ReadCodeAsync()).ShouldBe("gallery.finals_incomplete");

        await UploadFinalsAsync(browser, path, [chosen[1], chosen[2]], bytes);
        (await (await browser.GetAsync($"{path}/selection", TestContext.Current.CancellationToken)).ReadJsonAsync())
            .GetProperty("photos").EnumerateArray().Select(photo => photo.GetProperty("finalBytes").GetInt64()).ShouldBe([bytes.Length, bytes.Length, bytes.Length]);
        var published = await PostAsync(browser, $"{path}/finals/publish", new { });
        published.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await published.ReadJsonAsync()).GetProperty("status").GetString().ShouldBe("Ready");

        await RunMaintenancePassAsync();
        var owed = await (await scenario.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}/gallery", session)).ReadJsonAsync();
        owed.GetProperty("status").GetString().ShouldBe("Ready");
        owed.GetProperty("finals").GetArrayLength().ShouldBe(0);

        (await scenario.Client.SendAsync(
            HttpMethod.Post, $"/api/bookings/{scenario.BookingId}/payments/in-person", scenario.Photographer.AccessToken,
            new { amount = 100000, currency = "CRC", method = "Cash", idempotencyKey = $"all-{scenario.BookingId:N}" })).EnsureSuccessStatusCode();
        System.Text.Json.JsonElement delivered = default;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await RunMaintenancePassAsync();
            delivered = await (await scenario.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}/gallery", session)).ReadJsonAsync();
            if (delivered.GetProperty("status").GetString() == "Delivered")
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        }

        delivered.GetProperty("status").GetString().ShouldBe("Delivered");
        var finals = delivered.GetProperty("finals").EnumerateArray().ToList();
        finals.Select(final => final.GetProperty("fileName").GetString()).ShouldBe(["IMG_0002.jpg", "IMG_0000.jpg"]);
        finals.ShouldAllBe(final => !final.GetProperty("isExtra").GetBoolean());
        finals.ShouldAllBe(final => final.EnumerateObject().All(property => property.Name != "url"));

        // The client asks for the addresses when they download, and each photo asked for is counted.
        var downloads = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/gallery/downloads", session, new { photoIds = new[] { chosen[0] } });
        downloads.StatusCode.ShouldBe(HttpStatusCode.OK);
        var link = (await downloads.ReadJsonAsync()).GetProperty("items")[0];
        link.GetProperty("fileName").GetString().ShouldBe("IMG_0002.jpg");
        var download = await new HttpClient().GetAsync(link.GetProperty("url").GetString(), TestContext.Current.CancellationToken);
        (await download.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).ShouldBe(bytes);
        var extraAsked = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/gallery/downloads", session, new { photoIds = new[] { chosen[2] } });
        extraAsked.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await extraAsked.ReadCodeAsync()).ShouldBe("gallery.invalid_photo");

        var seen = (await (await browser.GetAsync($"{path}/selection", TestContext.Current.CancellationToken)).ReadJsonAsync()).GetProperty("photos").EnumerateArray().ToList();
        seen.Select(photo => photo.GetProperty("finalDownloads").GetInt32()).ShouldBe([1, 0, 0]);
        seen[0].GetProperty("finalWidth").GetInt32().ShouldBe(1250);
        seen[0].GetProperty("finalHeight").GetInt32().ShouldBe(3000);
        seen[0].TryGetProperty("finalCapturedAt", out var capturedAt).ShouldBeTrue();
        capturedAt.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.String);
        var mine = await (await browser.GetAsync($"{path}/finals/{chosen[0]}/download", TestContext.Current.CancellationToken)).ReadJsonAsync();
        (await new HttpClient().GetAsync(mine.GetProperty("url").GetString(), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await (await browser.GetAsync($"{path}/selection", TestContext.Current.CancellationToken)).ReadJsonAsync()).GetProperty("photos")[0].GetProperty("finalDownloads").GetInt32().ShouldBe(1);

        // The ZIP, made by hand: uploaded, offered to the client with its size, counted when downloaded, and deleted.
        var zipBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4 };
        (await PostAsync(browser, $"{path}/zip/upload", new { bytes = Gallery.MaxZipBytes + 1 })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var slot = await (await PostAsync(browser, $"{path}/zip/upload", new { bytes = zipBytes.Length })).ReadJsonAsync();
        using (var put = new HttpRequestMessage(HttpMethod.Put, slot.GetProperty("url").GetString()) { Content = new ByteArrayContent(zipBytes) })
        {
            put.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
            (await new HttpClient().SendAsync(put, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var withZip = await PostAsync(browser, $"{path}/zip", new { });
        withZip.StatusCode.ShouldBe(HttpStatusCode.OK);
        var zipInfo = (await withZip.ReadJsonAsync()).GetProperty("zip");
        zipInfo.GetProperty("bytes").GetInt64().ShouldBe(zipBytes.Length);
        zipInfo.GetProperty("downloads").GetInt32().ShouldBe(0);
        var offered = await (await scenario.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}/gallery", session)).ReadJsonAsync();
        offered.GetProperty("zip").GetProperty("bytes").GetInt64().ShouldBe(zipBytes.Length);
        var zipLink = await (await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/gallery/zip/download", session, new { })).ReadJsonAsync();
        zipLink.GetProperty("bytes").GetInt64().ShouldBe(zipBytes.Length);
        (await (await new HttpClient().GetAsync(zipLink.GetProperty("url").GetString(), TestContext.Current.CancellationToken)).Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).ShouldBe(zipBytes);
        var counted = (await (await browser.GetAsync(path, TestContext.Current.CancellationToken)).ReadJsonAsync()).GetProperty("zip");
        counted.GetProperty("downloads").GetInt32().ShouldBe(1);
        counted.GetProperty("lastDownloadedAt").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.String);

        (await PostAsync(browser, $"{path}/zip/remove", new { })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await (await scenario.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}/gallery", session)).ReadJsonAsync()).TryGetProperty("zip", out var gone).ShouldBeTrue();
        gone.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        (await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/gallery/zip/download", session, new { })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await PostAsync(browser, $"{path}/extras/release", new { })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var released = await (await scenario.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}/gallery", session)).ReadJsonAsync();
        released.GetProperty("extrasReleased").GetBoolean().ShouldBeTrue();
        released.GetProperty("finals").EnumerateArray().Select(final => final.GetProperty("isExtra").GetBoolean()).ShouldBe([false, false, true]);
    }

    /// <summary>
    /// Asks for places to upload the finals of some photos, uploads the bytes to them and registers them.
    /// </summary>
    /// <param name="browser">The browser that is signed in.</param>
    /// <param name="galleryPath">Path of the gallery in the web area.</param>
    /// <param name="photoIds">The photos the finals belong to.</param>
    /// <param name="bytes">The content of every final.</param>
    /// <returns>A task that completes when they were registered.</returns>
    private static async Task UploadFinalsAsync(HttpClient browser, string galleryPath, IReadOnlyList<Guid> photoIds, byte[] bytes)
    {
        var slots = await PostAsync(browser, $"{galleryPath}/finals/uploads", new { files = photoIds.Select(id => new { photoId = id, bytes = bytes.Length }) });
        slots.StatusCode.ShouldBe(HttpStatusCode.OK);
        foreach (var slot in (await slots.ReadJsonAsync()).GetProperty("slots").EnumerateArray())
        {
            using var put = new HttpRequestMessage(HttpMethod.Put, slot.GetProperty("url").GetString()) { Content = new ByteArrayContent(bytes) };
            put.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            (await new HttpClient().SendAsync(put, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var details = photoIds.Select(id => new { photoId = id, width = 1250, height = 3000, capturedAt = DateTimeOffset.UtcNow.AddDays(-3) });
        var registered = await PostAsync(browser, $"{galleryPath}/finals", new { photoIds, details });
        registered.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await registered.ReadJsonAsync()).GetProperty("rejected").GetArrayLength().ShouldBe(0);
    }

    /// <summary>
    /// Creates a photographer, a booking with a client email and its link, and a gallery of that booking in the given state.
    /// </summary>
    /// <param name="status">State of the gallery.</param>
    /// <param name="photos">Proofs the gallery has.</param>
    /// <param name="included">Photos the package includes.</param>
    /// <returns>The scenario.</returns>
    private async Task<Scenario> StartAsync(GalleryStatus status, int photos, int included)
    {
        var user = await fixture.CreateUserAsync(verifiedEmail: true);
        var client = fixture.CreateClient();
        var photographer = await client.LoginAsync(user);
        var clientEmail = $"c{Guid.NewGuid():N}@example.com";
        var booking = await client.CreateBookingAsync(photographer.AccessToken, new Dictionary<string, object> { ["clientEmail"] = clientEmail });
        var bookingId = booking.GetProperty("id").GetGuid();
        var link = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{bookingId}/link", photographer.AccessToken);
        link.StatusCode.ShouldBe(HttpStatusCode.Created);
        var token = (await link.ReadJsonAsync()).GetProperty("token").GetString()!;

        var gallery = Gallery.Restore(
            Guid.CreateVersion7(),
            0,
            user.Id,
            bookingId,
            new GallerySnapshot("María Pérez", "Retrato Familiar", DateTimeOffset.UtcNow),
            status,
            included,
            0,
            Money.Create(3_000m, "CRC"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            Enumerable.Range(0, photos).Select(index => new GalleryPhoto(Guid.CreateVersion7(), $"IMG_{index:0000}.jpg", 100, DateTimeOffset.UtcNow)));
        using var scope = fixture.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IGalleryRepository>().TryAddAsync(gallery, TestContext.Current.CancellationToken)).ShouldBeTrue();
        return new Scenario(client, user, photographer, bookingId, token, clientEmail, gallery.Id);
    }

    /// <summary>
    /// Waits for the notice of the selection to reach the inbox of the photographer; the API runs the outbox by itself after a change.
    /// </summary>
    /// <param name="scenario">The scenario.</param>
    /// <returns>The notice.</returns>
    private async Task<System.Text.Json.JsonElement> WaitForNoticeAsync(Scenario scenario)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await RunMaintenancePassAsync();
            var inbox = await (await scenario.Client.SendAsync(HttpMethod.Get, "/api/notifications", scenario.Photographer.AccessToken)).ReadJsonAsync();
            foreach (var item in inbox.GetProperty("items").EnumerateArray())
            {
                if (item.GetProperty("type").GetString() == "SelectionSubmitted")
                {
                    return item;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        }

        throw new InvalidOperationException("The notice of the selection did not reach the inbox.");
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
    /// Sends a POST from the site, with its origin.
    /// </summary>
    /// <param name="client">The browser.</param>
    /// <param name="path">Path of the request.</param>
    /// <param name="body">JSON body.</param>
    /// <returns>The response.</returns>
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Origin", SiteOrigin);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Skips the test when MongoDB or the object storage is not reachable.
    /// </summary>
    private void Assume() =>
        Assert.SkipUnless(fixture.IsAvailable && fixture.StorageAvailable, "MongoDB or the object storage is not reachable; run 'docker compose up -d' in the backend folder.");

    /// <summary>
    /// A photographer, a booking with its link and a gallery of it, ready for a test to act as the client.
    /// </summary>
    /// <param name="Client">HTTP client, with its own address for the rate limits.</param>
    /// <param name="User">The photographer, to sign in on the web.</param>
    /// <param name="Photographer">Session of the photographer in the app.</param>
    /// <param name="BookingId">Booking identifier.</param>
    /// <param name="Token">Secret token of the link.</param>
    /// <param name="ClientEmail">Email registered for the client.</param>
    /// <param name="GalleryId">Gallery identifier.</param>
    private sealed record Scenario(HttpClient Client, TestUser User, SessionTokens Photographer, Guid BookingId, string Token, string ClientEmail, Guid GalleryId)
    {
        /// <summary>
        /// Asks for a code, reads it from the captured email and exchanges it for a portal session.
        /// </summary>
        /// <param name="fixture">Shared API instance, whose captured emails hold the code.</param>
        /// <returns>The portal access token.</returns>
        public async Task<string> OpenPortalSessionAsync(ApiFixture fixture)
        {
            (await Client.SendAsync(HttpMethod.Post, $"/public/bookings/{Token}/code", null)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
            var verified = await Client.SendAsync(HttpMethod.Post, $"/public/bookings/{Token}/code/verify", null, new { code = fixture.Emails.LastCodeFor(ClientEmail) });
            verified.StatusCode.ShouldBe(HttpStatusCode.OK);
            return (await verified.ReadJsonAsync()).GetProperty("accessToken").GetString()!;
        }
    }
}
