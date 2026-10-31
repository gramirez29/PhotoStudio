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
        var sent = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/gallery/selection", session, new { photoIds = chosen });
        sent.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sentBody = await sent.ReadJsonAsync();
        sentBody.GetProperty("status").GetString().ShouldBe("InEditing");
        sentBody.GetProperty("selection").GetProperty("extraPhotos").GetInt32().ShouldBe(1);
        sentBody.GetProperty("selection").GetProperty("extraTotal").GetProperty("amount").GetDecimal().ShouldBe(3_000m);
        sentBody.GetProperty("selection").GetProperty("photoIds").EnumerateArray().Select(id => id.GetGuid()).ShouldBe(chosen);

        (await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/gallery/selection", session, new { photoIds = chosen.Reverse() })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var another = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/gallery/selection", session, new { photoIds = new[] { ids[1] } });
        another.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await another.ReadCodeAsync()).ShouldBe("gallery.invalid_state");

        var gallery = await (await scenario.Client.SendAsync(HttpMethod.Get, $"/api/galleries/{scenario.GalleryId}", scenario.Photographer.AccessToken)).ReadJsonAsync();
        gallery.GetProperty("status").GetString().ShouldBe("InEditing");
        gallery.GetProperty("selectedPhotoCount").GetInt32().ShouldBe(3);
        gallery.GetProperty("extraPhotoCount").GetInt32().ShouldBe(1);
        gallery.GetProperty("allowedActions").GetArrayLength().ShouldBe(0);

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
