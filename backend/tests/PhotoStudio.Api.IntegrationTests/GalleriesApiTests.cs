using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Maintenance.RunMaintenance;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the gallery endpoints: they need a session, one photographer never sees the galleries of another, the
/// list is paged, and the terms can be edited with the right errors.
/// </summary>
/// <param name="fixture">Shared API host.</param>
[Collection(ApiCollection.Name)]
public sealed class GalleriesApiTests(ApiFixture fixture)
{
    /// <summary>
    /// Every gallery endpoint refuses a request without a session.
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Request path.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("GET", "/api/galleries")]
    [InlineData("GET", "/api/galleries/0197a000-0000-7000-8000-000000000099")]
    [InlineData("GET", "/api/galleries/by-booking/0197a000-0000-7000-8000-000000000099")]
    [InlineData("POST", "/api/galleries/0197a000-0000-7000-8000-000000000099/terms")]
    public async Task EveryEndpoint_RequiresASession(string method, string path)
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");

        var response = await fixture.CreateClient().SendAsync(new HttpMethod(method), path, accessToken: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The owner reads the gallery by identifier and by booking, with its terms and allowed actions; another photographer gets
    /// 404 on both, exactly as for a gallery that does not exist.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheOwnerReadsTheGallery_AndAnotherPhotographerCannot()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var other = await client.LoginAsync(await fixture.CreateUserAsync());
        var gallery = await AddGalleryAsync(owner.PhotographerId, DateTimeOffset.UtcNow);

        var byId = await (await client.SendAsync(HttpMethod.Get, $"/api/galleries/{gallery.Id}", owner.AccessToken)).ReadJsonAsync();
        var byBooking = await (await client.SendAsync(HttpMethod.Get, $"/api/galleries/by-booking/{gallery.BookingId}", owner.AccessToken)).ReadJsonAsync();

        byId.GetProperty("id").GetGuid().ShouldBe(gallery.Id);
        byBooking.GetProperty("id").GetGuid().ShouldBe(gallery.Id);
        byId.GetProperty("status").GetString().ShouldBe("Pending");
        byId.GetProperty("clientName").GetString().ShouldBe("María Pérez");
        byId.GetProperty("includedPhotos").GetInt32().ShouldBe(30);
        byId.GetProperty("selectablePhotos").GetInt32().ShouldBe(30);
        byId.GetProperty("maxPhotos").GetInt32().ShouldBe(150);
        byId.GetProperty("extraPhotoPrice").GetProperty("amount").GetDecimal().ShouldBe(3_000m);
        byId.GetProperty("allowedActions").EnumerateArray().Select(action => action.GetString()).ShouldBe(["EditTerms", "UploadProofs"]);

        (await client.SendAsync(HttpMethod.Get, $"/api/galleries/{gallery.Id}", other.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.SendAsync(HttpMethod.Get, $"/api/galleries/by-booking/{gallery.BookingId}", other.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.SendAsync(HttpMethod.Get, $"/api/galleries/{Guid.CreateVersion7()}", owner.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The list comes five at a time, the most recently created first, only with the photographer's galleries, and following
    /// the cursor visits each one once.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheList_ComesInPagesOfFive_NewestFirst_OnlyWithOwnGalleries()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var other = await client.LoginAsync(await fixture.CreateUserAsync());
        var created = new List<Guid>();
        for (var index = 0; index < 7; index++)
        {
            created.Add((await AddGalleryAsync(owner.PhotographerId, DateTimeOffset.UtcNow.AddMinutes(index))).Id);
        }

        await AddGalleryAsync(other.PhotographerId, DateTimeOffset.UtcNow);

        var first = await (await client.SendAsync(HttpMethod.Get, "/api/galleries", owner.AccessToken)).ReadJsonAsync();
        var cursor = first.GetProperty("nextCursor").GetString();
        var second = await (await client.SendAsync(HttpMethod.Get, $"/api/galleries?cursor={cursor}", owner.AccessToken)).ReadJsonAsync();

        Ids(first).Count.ShouldBe(5);
        Ids(first).Concat(Ids(second)).ShouldBe([.. created.AsEnumerable().Reverse()]);
        second.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
        (await client.SendAsync(HttpMethod.Get, "/api/galleries?cursor=garbage", owner.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The photographer changes the terms, and what they read afterwards is the new terms, with the gift counted.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task EditTerms_ChangesTheTermsOfTheOwnersGallery()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var gallery = await AddGalleryAsync(owner.PhotographerId, DateTimeOffset.UtcNow);

        var response = await client.SendAsync(
            HttpMethod.Post, $"/api/galleries/{gallery.Id}/terms", owner.AccessToken, new { includedPhotos = 30, bonusPhotos = 5, extraPhotoPrice = 2500 });
        var read = await (await client.SendAsync(HttpMethod.Get, $"/api/galleries/{gallery.Id}", owner.AccessToken)).ReadJsonAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        read.GetProperty("bonusPhotos").GetInt32().ShouldBe(5);
        read.GetProperty("selectablePhotos").GetInt32().ShouldBe(35);
        read.GetProperty("extraPhotoPrice").GetProperty("amount").GetDecimal().ShouldBe(2_500m);
    }

    /// <summary>
    /// Another photographer cannot change the terms: 404, and nothing changes.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task EditTerms_OfAnotherPhotographersGallery_IsNotFoundAndChangesNothing()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var other = await client.LoginAsync(await fixture.CreateUserAsync());
        var gallery = await AddGalleryAsync(owner.PhotographerId, DateTimeOffset.UtcNow);

        var response = await client.SendAsync(
            HttpMethod.Post, $"/api/galleries/{gallery.Id}/terms", other.AccessToken, new { includedPhotos = 1, bonusPhotos = 0, extraPhotoPrice = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var read = await (await client.SendAsync(HttpMethod.Get, $"/api/galleries/{gallery.Id}", owner.AccessToken)).ReadJsonAsync();
        read.GetProperty("includedPhotos").GetInt32().ShouldBe(30);
    }

    /// <summary>
    /// Invalid terms are 422 with a stable code, and once the selection was sent the terms are 409.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task EditTerms_WithInvalidValuesOrAFrozenGallery_AnswersWithAStableCode()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var open = await AddGalleryAsync(owner.PhotographerId, DateTimeOffset.UtcNow);
        var frozen = await AddGalleryAsync(owner.PhotographerId, DateTimeOffset.UtcNow, GalleryStatus.InEditing);

        var tooMany = await client.SendAsync(HttpMethod.Post, $"/api/galleries/{open.Id}/terms", owner.AccessToken, new { includedPhotos = 100, bonusPhotos = 51, extraPhotoPrice = 3000 });
        var negativePrice = await client.SendAsync(HttpMethod.Post, $"/api/galleries/{open.Id}/terms", owner.AccessToken, new { includedPhotos = 10, bonusPhotos = 0, extraPhotoPrice = -1 });
        var afterSelection = await client.SendAsync(HttpMethod.Post, $"/api/galleries/{frozen.Id}/terms", owner.AccessToken, new { includedPhotos = 10, bonusPhotos = 0, extraPhotoPrice = 3000 });

        tooMany.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await tooMany.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe("gallery.invalid_terms");
        negativePrice.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await negativePrice.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe("money.invalid_amount");
        afterSelection.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await afterSelection.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe("gallery.invalid_state");
    }

    /// <summary>
    /// The whole path: a confirmed booking whose session starts is completed by the photographer, and the gallery appears by
    /// itself after the outbox runs, with the photos of the package and the client of the booking; it is the owner's alone, and
    /// running the pass again does not create a second one.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CompletingTheSession_CreatesTheGalleryByItself_OnlyForTheOwner()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var other = await client.LoginAsync(await fixture.CreateUserAsync());
        // A session that starts in a few seconds: a session cannot be completed before it starts, and the test cannot move the clock.
        var start = DateTimeOffset.UtcNow.AddSeconds(4);
        var booking = await client.CreateBookingAsync(
            owner.AccessToken,
            new Dictionary<string, object> { ["sessionStart"] = start, ["sessionEnd"] = start.AddHours(1), ["packageTier"] = "Standard" });
        var bookingId = booking.GetProperty("id").GetGuid();
        (await client.SendAsync(
            HttpMethod.Post, $"/api/bookings/{bookingId}/contract/in-person", owner.AccessToken, new { signerName = "María Pérez", templateVersion = "v1", isPaperContract = false })).EnsureSuccessStatusCode();
        (await client.SendAsync(
            HttpMethod.Post, $"/api/bookings/{bookingId}/payments/in-person", owner.AccessToken, new { amount = 50000, currency = "CRC", method = "Cash", idempotencyKey = $"deposit-{bookingId:N}" })).EnsureSuccessStatusCode();
        (await client.SendAsync(HttpMethod.Get, $"/api/galleries/by-booking/{bookingId}", owner.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        (await client.SendAsync(HttpMethod.Post, $"/api/bookings/{bookingId}/complete", owner.AccessToken)).EnsureSuccessStatusCode();
        var response = await WaitForGalleryAsync(client, owner.AccessToken, bookingId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await DescribeOutboxAsync());
        var gallery = await response.ReadJsonAsync();
        gallery.GetProperty("status").GetString().ShouldBe("Pending");
        gallery.GetProperty("clientName").GetString().ShouldBe("María Pérez");
        gallery.GetProperty("includedPhotos").GetInt32().ShouldBe(30);
        gallery.GetProperty("extraPhotoPrice").GetProperty("amount").GetDecimal().ShouldBe(3_000m);
        (await client.SendAsync(HttpMethod.Get, $"/api/galleries/by-booking/{bookingId}", other.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var list = await (await client.SendAsync(HttpMethod.Get, "/api/galleries", owner.AccessToken)).ReadJsonAsync();
        Ids(list).ShouldBe([gallery.GetProperty("id").GetGuid()]);
    }

    /// <summary>
    /// Reads the identifiers of a page of galleries, in order.
    /// </summary>
    /// <param name="page">The page as JSON.</param>
    /// <returns>The identifiers.</returns>
    private static List<Guid> Ids(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

    /// <summary>
    /// Asks for the gallery of a booking until it exists, running a maintenance pass between tries: the API also delivers the outbox
    /// by itself after each change, so the message may be in the hands of that pass for a moment.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="accessToken">Access token of the photographer.</param>
    /// <param name="bookingId">Booking whose gallery is expected.</param>
    /// <returns>The last response, which is the gallery if it appeared within 15 seconds.</returns>
    private async Task<HttpResponseMessage> WaitForGalleryAsync(HttpClient client, string accessToken, Guid bookingId)
    {
        HttpResponseMessage response;
        var tries = 0;
        do
        {
            await RunMaintenancePassAsync();
            response = await client.SendAsync(HttpMethod.Get, $"/api/galleries/by-booking/{bookingId}", accessToken);
            if (response.StatusCode == HttpStatusCode.OK || ++tries >= 30)
            {
                return response;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        }
        while (true);
    }

    /// <summary>
    /// Describes the messages of the outbox that were not processed, for the message of a failed check.
    /// </summary>
    /// <returns>One line per message: its type, status, attempts and last error.</returns>
    private async Task<string> DescribeOutboxAsync()
    {
        var messages = await fixture.Database.GetCollection<BsonDocument>("outbox_messages")
            .Find(Builders<BsonDocument>.Filter.Ne("status", "Processed"))
            .ToListAsync(TestContext.Current.CancellationToken);
        return "Outbox: " + string.Join(" | ", messages.Select(message =>
            $"{message["type"]} {message["status"]} attempts={message["attempts"]} error={message.GetValue("lastError", BsonNull.Value)}"));
    }

    /// <summary>
    /// Runs one maintenance pass in-process, the way the worker does, without using the rate-limited endpoint.
    /// </summary>
    /// <returns>A task that completes when the pass ends.</returns>
    private async Task RunMaintenancePassAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICommandHandler<RunMaintenanceCommand, MaintenanceResponse>>()
            .HandleAsync(new RunMaintenanceCommand(), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Stores a gallery of a photographer, with 30 included photos and ₡3 000 per extra photo, straight in the repository (a
    /// gallery is created by the system when a session is completed, and a session cannot be completed before it starts).
    /// </summary>
    /// <param name="photographerId">The photographer.</param>
    /// <param name="createdAt">Creation instant.</param>
    /// <param name="status">The state, when it is not the initial one.</param>
    /// <returns>The gallery.</returns>
    private async Task<Gallery> AddGalleryAsync(Guid photographerId, DateTimeOffset createdAt, GalleryStatus status = GalleryStatus.Pending)
    {
        var created = Gallery.Create(
            photographerId,
            Guid.CreateVersion7(),
            new GallerySnapshot("María Pérez", "Retrato Familiar", createdAt.AddDays(-1)),
            30,
            Money.Create(3_000m, "CRC"),
            createdAt);
        var gallery = status == GalleryStatus.Pending
            ? created
            : Gallery.Restore(created.Id, 0, photographerId, created.BookingId, created.Snapshot, status, 30, 0, created.ExtraPhotoPrice, createdAt, createdAt);

        using var scope = fixture.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IGalleryRepository>().TryAddAsync(gallery, TestContext.Current.CancellationToken)).ShouldBeTrue();
        return gallery;
    }
}
