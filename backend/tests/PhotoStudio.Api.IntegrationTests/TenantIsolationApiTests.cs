using System.Net;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of tenant isolation: the photographer comes only from the access token, so one photographer can neither
/// create data for another nor read or change what another owns.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class TenantIsolationApiTests(ApiFixture fixture)
{
    /// <summary>
    /// A booking is created under the photographer of the token, even if the body carries another photographer.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CreateBooking_UsesThePhotographerOfTheTokenAndIgnoresTheBody()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var forgedOwner = Guid.CreateVersion7();

        var booking = await client.CreateBookingAsync(session.AccessToken, new Dictionary<string, object> { ["photographerId"] = forgedOwner });

        booking.GetProperty("photographerId").GetGuid().ShouldBe(session.PhotographerId);
        booking.GetProperty("photographerId").GetGuid().ShouldNotBe(forgedOwner);
    }

    /// <summary>
    /// The list shows only the bookings of the signed-in photographer, and a <c>photographerId</c> in the query string is ignored.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListBookings_ShowsOnlyOwnBookingsAndIgnoresTheQueryString()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var other = await client.LoginAsync(await fixture.CreateUserAsync());
        var ownerBooking = await client.CreateBookingAsync(owner.AccessToken);

        var ownList = await client.SendAsync(HttpMethod.Get, "/api/bookings", owner.AccessToken);
        var otherList = await client.SendAsync(HttpMethod.Get, $"/api/bookings?photographerId={owner.PhotographerId}", other.AccessToken);

        (await ownList.ReadJsonAsync()).EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
            .ShouldContain(ownerBooking.GetProperty("id").GetGuid());
        (await otherList.ReadJsonAsync()).GetArrayLength().ShouldBe(0);
    }

    /// <summary>
    /// Another photographer gets 404 on every operation over a booking they do not own: the same answer as a booking that
    /// does not exist, so the identifier is not even confirmed to exist. The owner still sees the booking unchanged.
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="suffix">Path after the booking identifier.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("GET", "")]
    [InlineData("POST", "/cancel")]
    [InlineData("POST", "/complete")]
    [InlineData("POST", "/client-absent")]
    [InlineData("POST", "/client-absent/revert")]
    [InlineData("POST", "/reschedule")]
    [InlineData("POST", "/contract/in-person")]
    [InlineData("POST", "/payments/in-person")]
    public async Task OperationsOnAnotherPhotographersBooking_Return404(string method, string suffix)
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var intruder = await client.LoginAsync(await fixture.CreateUserAsync());
        var booking = await client.CreateBookingAsync(owner.AccessToken);
        var id = booking.GetProperty("id").GetGuid();
        object? body = suffix switch
        {
            "/cancel" => new { reason = "x" },
            "/client-absent/revert" => new { reason = "x" },
            "/reschedule" => new { sessionStart = DateTimeOffset.UtcNow.AddDays(20), sessionEnd = DateTimeOffset.UtcNow.AddDays(20).AddHours(2) },
            "/contract/in-person" => new { signerName = "María", templateVersion = "v1", isPaperContract = false },
            "/payments/in-person" => new { amount = 50000, currency = "CRC", method = "Cash", idempotencyKey = "k" },
            _ => null,
        };

        var response = await client.SendAsync(new HttpMethod(method), $"/api/bookings/{id}{suffix}", intruder.AccessToken, body);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadCodeAsync()).ShouldBe("resource.not_found");
        var unchanged = await client.SendAsync(HttpMethod.Get, $"/api/bookings/{id}", owner.AccessToken);
        unchanged.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await unchanged.ReadJsonAsync()).GetProperty("status").GetString().ShouldBe("Tentative");
    }

    /// <summary>
    /// The owner can operate on their own booking, so the 404s above are caused by ownership and not by a broken endpoint.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheOwner_CanReadAndCancelTheirOwnBooking()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var id = (await client.CreateBookingAsync(owner.AccessToken)).GetProperty("id").GetGuid();

        var read = await client.SendAsync(HttpMethod.Get, $"/api/bookings/{id}", owner.AccessToken);
        var cancel = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/cancel", owner.AccessToken, new { });

        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await cancel.ReadJsonAsync()).GetProperty("status").GetString().ShouldBe("Cancelled");
    }

    /// <summary>
    /// A booking that does not exist answers exactly like one that belongs to someone else.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AMissingBooking_AnswersLikeSomeoneElsesBooking()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var intruder = await client.LoginAsync(await fixture.CreateUserAsync());
        var id = (await client.CreateBookingAsync(owner.AccessToken)).GetProperty("id").GetGuid();

        var someoneElses = await client.SendAsync(HttpMethod.Get, $"/api/bookings/{id}", intruder.AccessToken);
        var missing = await client.SendAsync(HttpMethod.Get, $"/api/bookings/{Guid.CreateVersion7()}", intruder.AccessToken);

        someoneElses.StatusCode.ShouldBe(missing.StatusCode);
        (await someoneElses.ReadCodeAsync()).ShouldBe(await missing.ReadCodeAsync());
    }
}
