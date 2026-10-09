using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Maintenance.RunMaintenance;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the notification inbox: access control, tenant isolation, and the whole path from a confirmed booking
/// through the outbox to a delivered reminder in the inbox.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class NotificationsApiTests(ApiFixture fixture)
{
    /// <summary>
    /// Every notification endpoint requires a signed-in user.
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Path of the endpoint.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("GET", "/api/notifications")]
    [InlineData("POST", "/api/notifications/read-all")]
    [InlineData("POST", "/api/notifications/0197a000-0000-7000-8000-000000000099/read")]
    public async Task Endpoints_WithoutToken_Return401(string method, string path)
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");

        var response = await fixture.CreateClient().SendAsync(new HttpMethod(method), path, accessToken: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A new photographer has an empty inbox, and reading a notification that does not exist answers 404.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task NewPhotographer_HasAnEmptyInbox()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());

        var inbox = await (await client.SendAsync(HttpMethod.Get, "/api/notifications", session.AccessToken)).ReadJsonAsync();
        var missing = await client.SendAsync(HttpMethod.Post, $"/api/notifications/{Guid.CreateVersion7()}/read", session.AccessToken);

        inbox.GetProperty("items").GetArrayLength().ShouldBe(0);
        inbox.GetProperty("unreadCount").GetInt32().ShouldBe(0);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A confirmed booking whose session is less than 24 hours away gets its reminder delivered by one maintenance pass; the
    /// owner sees and reads it, and another photographer can neither see it nor read it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ConfirmedBooking_ProducesAReminderOnlyTheOwnerCanSeeAndRead()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var other = await client.LoginAsync(await fixture.CreateUserAsync());
        var start = DateTimeOffset.UtcNow.AddHours(10);
        var booking = await client.CreateBookingAsync(
            owner.AccessToken,
            new Dictionary<string, object> { ["sessionStart"] = start, ["sessionEnd"] = start.AddHours(2) });
        var bookingId = booking.GetProperty("id").GetGuid();

        (await client.SendAsync(
            HttpMethod.Post,
            $"/api/bookings/{bookingId}/contract/in-person",
            owner.AccessToken,
            new { signerName = "María Pérez", templateVersion = "v1", isPaperContract = false })).EnsureSuccessStatusCode();
        (await client.SendAsync(
            HttpMethod.Post,
            $"/api/bookings/{bookingId}/payments/in-person",
            owner.AccessToken,
            new { amount = 50000, currency = "CRC", method = "Cash", idempotencyKey = "deposit-1" })).EnsureSuccessStatusCode();

        await RunMaintenancePassAsync();

        var inbox = await (await client.SendAsync(HttpMethod.Get, "/api/notifications", owner.AccessToken)).ReadJsonAsync();
        var item = inbox.GetProperty("items").EnumerateArray().Single(notification => notification.GetProperty("bookingId").GetGuid() == bookingId);
        item.GetProperty("type").GetString().ShouldBe("SessionReminder");
        item.GetProperty("clientName").GetString().ShouldBe("María Pérez");
        item.GetProperty("readAt").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        inbox.GetProperty("unreadCount").GetInt32().ShouldBe(1);

        var foreignInbox = await (await client.SendAsync(HttpMethod.Get, "/api/notifications", other.AccessToken)).ReadJsonAsync();
        foreignInbox.GetProperty("items").GetArrayLength().ShouldBe(0);
        var id = item.GetProperty("id").GetGuid();
        (await client.SendAsync(HttpMethod.Post, $"/api/notifications/{id}/read", other.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var read = await client.SendAsync(HttpMethod.Post, $"/api/notifications/{id}/read", owner.AccessToken);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await read.ReadJsonAsync()).GetProperty("readAt").ValueKind.ShouldNotBe(System.Text.Json.JsonValueKind.Null);
        var after = await (await client.SendAsync(HttpMethod.Get, "/api/notifications", owner.AccessToken)).ReadJsonAsync();
        after.GetProperty("unreadCount").GetInt32().ShouldBe(0);

        // Running the pass again delivers nothing new: the job is idempotent.
        await RunMaintenancePassAsync();
        var again = await (await client.SendAsync(HttpMethod.Get, "/api/notifications", owner.AccessToken)).ReadJsonAsync();
        again.GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    /// <summary>
    /// A reminder that was scheduled is dropped, not delivered, when the booking is cancelled before it comes due.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CancelledBooking_ProducesNoReminder()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var start = DateTimeOffset.UtcNow.AddHours(10);
        var booking = await client.CreateBookingAsync(
            owner.AccessToken,
            new Dictionary<string, object> { ["sessionStart"] = start, ["sessionEnd"] = start.AddHours(2) });
        var bookingId = booking.GetProperty("id").GetGuid();
        (await client.SendAsync(
            HttpMethod.Post,
            $"/api/bookings/{bookingId}/contract/in-person",
            owner.AccessToken,
            new { signerName = "María Pérez", templateVersion = "v1", isPaperContract = false })).EnsureSuccessStatusCode();
        (await client.SendAsync(
            HttpMethod.Post,
            $"/api/bookings/{bookingId}/payments/in-person",
            owner.AccessToken,
            new { amount = 50000, currency = "CRC", method = "Cash", idempotencyKey = "deposit-1" })).EnsureSuccessStatusCode();
        (await client.SendAsync(HttpMethod.Post, $"/api/bookings/{bookingId}/cancel", owner.AccessToken, new { reason = "Client asked" })).EnsureSuccessStatusCode();

        await RunMaintenancePassAsync();

        var inbox = await (await client.SendAsync(HttpMethod.Get, "/api/notifications", owner.AccessToken)).ReadJsonAsync();
        inbox.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    /// <summary>
    /// Runs one maintenance pass in-process, the way the worker does, without using the rate-limited endpoint.
    /// </summary>
    /// <returns>A task that completes when the pass ends.</returns>
    private async Task RunMaintenancePassAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<RunMaintenanceCommand, MaintenanceResponse>>();
        await handler.HandleAsync(new RunMaintenanceCommand(), TestContext.Current.CancellationToken);
    }
}
