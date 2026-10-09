using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Maintenance.RunMaintenance;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of billing: a booking that ends with money paid produces a pending refund through the outbox, which the
/// photographer sees, completes once, and nobody else can see or touch.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class BillingApiTests(ApiFixture fixture)
{
    /// <summary>
    /// Every billing endpoint requires a signed-in user.
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Path of the endpoint.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("GET", "/api/billing/refunds")]
    [InlineData("GET", "/api/billing/settlements/0197a000-0000-7000-8000-000000000099")]
    [InlineData("POST", "/api/billing/settlements/0197a000-0000-7000-8000-000000000099/refund/complete")]
    public async Task Endpoints_WithoutToken_Return401(string method, string path)
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");

        var response = await fixture.CreateClient().SendAsync(new HttpMethod(method), path, accessToken: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The whole path: confirmed booking with the deposit paid, cancelled by the photographer, a pending refund of the deposit
    /// appears after the outbox runs, is invisible to another photographer, and completes exactly once.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CancelledConfirmedBooking_ProducesARefundThatIsCompletedOnce()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var other = await client.LoginAsync(await fixture.CreateUserAsync());
        var bookingId = await CreateConfirmedBookingAsync(client, owner.AccessToken);
        (await client.SendAsync(HttpMethod.Post, $"/api/bookings/{bookingId}/cancel", owner.AccessToken, new { reason = "Sick" })).EnsureSuccessStatusCode();

        await RunMaintenancePassAsync();
        await RunMaintenancePassAsync();

        var refunds = await (await client.SendAsync(HttpMethod.Get, "/api/billing/refunds", owner.AccessToken)).ReadJsonAsync();
        refunds.GetProperty("pendingCount").GetInt32().ShouldBe(1);
        var item = refunds.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("bookingId").GetGuid().ShouldBe(bookingId);
        item.GetProperty("reason").GetString().ShouldBe("PhotographerCancelled");
        item.GetProperty("refundAmount").GetProperty("amount").GetDecimal().ShouldBe(50_000m);
        item.GetProperty("refundStatus").GetString().ShouldBe("Pending");
        item.GetProperty("clientName").GetString().ShouldBe("María Pérez");

        var foreign = await client.SendAsync(HttpMethod.Get, "/api/billing/refunds", other.AccessToken);
        (await foreign.ReadJsonAsync()).GetProperty("pendingCount").GetInt32().ShouldBe(0);
        (await client.SendAsync(HttpMethod.Get, $"/api/billing/settlements/{bookingId}", other.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.SendAsync(HttpMethod.Post, $"/api/billing/settlements/{bookingId}/refund/complete", other.AccessToken, new { method = "Cash" }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var card = await client.SendAsync(HttpMethod.Post, $"/api/billing/settlements/{bookingId}/refund/complete", owner.AccessToken, new { method = "Card" });
        card.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await card.ReadCodeAsync()).ShouldBe("payment.invalid_method");

        var done = await client.SendAsync(
            HttpMethod.Post, $"/api/billing/settlements/{bookingId}/refund/complete", owner.AccessToken, new { method = "SinpeMovil", note = "ref 123" });
        done.StatusCode.ShouldBe(HttpStatusCode.OK);
        var completed = await done.ReadJsonAsync();
        completed.GetProperty("refundStatus").GetString().ShouldBe("Completed");
        completed.GetProperty("refundMethod").GetString().ShouldBe("SinpeMovil");
        completed.GetProperty("refundNote").GetString().ShouldBe("ref 123");

        var again = await client.SendAsync(
            HttpMethod.Post, $"/api/billing/settlements/{bookingId}/refund/complete", owner.AccessToken, new { method = "Cash" });
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await again.ReadCodeAsync()).ShouldBe("booking.invalid_transition");

        var after = await (await client.SendAsync(HttpMethod.Get, "/api/billing/refunds", owner.AccessToken)).ReadJsonAsync();
        after.GetProperty("pendingCount").GetInt32().ShouldBe(0);
        var settlement = await (await client.SendAsync(HttpMethod.Get, $"/api/billing/settlements/{bookingId}", owner.AccessToken)).ReadJsonAsync();
        settlement.GetProperty("refundStatus").GetString().ShouldBe("Completed");
    }

    /// <summary>
    /// A booking cancelled before anything was paid has nothing to settle.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CancelledBookingWithoutPayments_HasNoSettlement()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var booking = await client.CreateBookingAsync(owner.AccessToken);
        var bookingId = booking.GetProperty("id").GetGuid();
        (await client.SendAsync(HttpMethod.Post, $"/api/bookings/{bookingId}/cancel", owner.AccessToken, new { })).EnsureSuccessStatusCode();

        await RunMaintenancePassAsync();

        (await client.SendAsync(HttpMethod.Get, $"/api/billing/settlements/{bookingId}", owner.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var refunds = await (await client.SendAsync(HttpMethod.Get, "/api/billing/refunds", owner.AccessToken)).ReadJsonAsync();
        refunds.GetProperty("pendingCount").GetInt32().ShouldBe(0);
    }

    /// <summary>
    /// Creates a booking and confirms it with the signed contract and the deposit paid in cash.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="accessToken">Access token of the photographer.</param>
    /// <returns>The booking identifier.</returns>
    private static async Task<Guid> CreateConfirmedBookingAsync(HttpClient client, string accessToken)
    {
        var booking = await client.CreateBookingAsync(accessToken);
        var bookingId = booking.GetProperty("id").GetGuid();
        (await client.SendAsync(
            HttpMethod.Post,
            $"/api/bookings/{bookingId}/contract/in-person",
            accessToken,
            new { signerName = "María Pérez", templateVersion = "v1", isPaperContract = false })).EnsureSuccessStatusCode();
        var paid = await client.SendAsync(
            HttpMethod.Post,
            $"/api/bookings/{bookingId}/payments/in-person",
            accessToken,
            new { amount = 50000, currency = "CRC", method = "Cash", idempotencyKey = $"deposit-{bookingId:N}" });
        paid.EnsureSuccessStatusCode();
        (await paid.ReadJsonAsync()).GetProperty("status").GetString().ShouldBe("Confirmed");
        return bookingId;
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
