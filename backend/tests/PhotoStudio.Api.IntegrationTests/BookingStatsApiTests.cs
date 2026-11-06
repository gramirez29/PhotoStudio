using System.Globalization;
using System.Net;
using System.Text.Json;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of <c>GET /api/bookings/stats</c>: the bookings counted by status for the home screen, who sees what, and what is refused.
/// </summary>
/// <param name="fixture">Shared API host.</param>
[Collection(ApiCollection.Name)]
public sealed class BookingStatsApiTests(ApiFixture fixture)
{
    private const string SkipReason = "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.";

    /// <summary>
    /// The statistics need a signed-in photographer.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Stats_WithoutAToken_IsUnauthorized()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);

        var response = await fixture.CreateClient().SendAsync(HttpMethod.Get, "/api/bookings/stats", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A photographer without bookings gets every status with zero, so the answer always has the same shape.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Stats_ForANewPhotographer_ListsEveryStatusWithZero()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());

        var response = await client.SendAsync(HttpMethod.Get, "/api/bookings/stats", session.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("total").GetInt32().ShouldBe(0);
        Statuses(json).Keys.ShouldBe(["Tentative", "Confirmed", "Completed", "Cancelled", "Expired", "ClientAbsent"], ignoreOrder: false);
        Statuses(json).Values.ShouldAllBe(count => count == 0);
    }

    /// <summary>
    /// The counts add up the photographer's own bookings by status, only inside the period, and never the ones of another photographer.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Stats_CountsTheOwnBookingsByStatusAndPeriod()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var other = await client.LoginAsync(await fixture.CreateUserAsync());
        var day = new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(60 + Random.Shared.Next(0, 200)), TimeSpan.Zero);
        await CreateAsync(client, owner.AccessToken, day.AddHours(9));
        var cancelled = await CreateAsync(client, owner.AccessToken, day.AddHours(13));
        await CreateAsync(client, owner.AccessToken, day.AddDays(10).AddHours(9));
        await CreateAsync(client, other.AccessToken, day.AddHours(9));
        (await client.SendAsync(HttpMethod.Post, $"/api/bookings/{cancelled}/cancel", owner.AccessToken, new { })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var inPeriod = await (await client.SendAsync(HttpMethod.Get, PeriodUrl(day, day.AddDays(1)), owner.AccessToken)).ReadJsonAsync();
        var all = await (await client.SendAsync(HttpMethod.Get, PeriodUrl(day, day.AddDays(30)), owner.AccessToken)).ReadJsonAsync();

        inPeriod.GetProperty("total").GetInt32().ShouldBe(2);
        Statuses(inPeriod)["Tentative"].ShouldBe(1);
        Statuses(inPeriod)["Cancelled"].ShouldBe(1);
        all.GetProperty("total").GetInt32().ShouldBe(3);
    }

    /// <summary>
    /// A period that ends before it starts is a bad request.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Stats_WithAnInvertedPeriod_IsABadRequest()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var now = DateTimeOffset.UtcNow;

        var response = await client.SendAsync(HttpMethod.Get, PeriodUrl(now, now.AddDays(-1)), session.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Builds the URL of a period, with the instants in ISO 8601 and encoded.
    /// </summary>
    /// <param name="from">Start of the period.</param>
    /// <param name="to">End of the period.</param>
    /// <returns>The path and query string.</returns>
    private static string PeriodUrl(DateTimeOffset from, DateTimeOffset to) =>
        $"/api/bookings/stats?from={Uri.EscapeDataString(from.ToString("O", CultureInfo.InvariantCulture))}&to={Uri.EscapeDataString(to.ToString("O", CultureInfo.InvariantCulture))}";

    /// <summary>
    /// Reads the counts of an answer, by status name, in the order they come.
    /// </summary>
    /// <param name="json">The answer.</param>
    /// <returns>The counts.</returns>
    private static Dictionary<string, int> Statuses(JsonElement json) =>
        json.GetProperty("byStatus").EnumerateArray().ToDictionary(item => item.GetProperty("status").GetString()!, item => item.GetProperty("count").GetInt32());

    /// <summary>
    /// Creates a tentative booking of two hours starting at the given instant and returns its identifier.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="accessToken">Access token of the photographer.</param>
    /// <param name="start">Session start.</param>
    /// <returns>The identifier of the booking.</returns>
    private static async Task<Guid> CreateAsync(HttpClient client, string accessToken, DateTimeOffset start)
    {
        var booking = await client.CreateBookingAsync(
            accessToken,
            new Dictionary<string, object> { ["sessionStart"] = start, ["sessionEnd"] = start.AddHours(2) });
        return booking.GetProperty("id").GetGuid();
    }
}
