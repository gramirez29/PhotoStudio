using System.Globalization;
using System.Net;
using System.Text.Json;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of <c>GET /api/bookings/calendar</c>: the bookings of a range for the calendar of the app, who can see them, which
/// ones come by default and what is refused.
/// </summary>
/// <param name="fixture">Shared API host.</param>
[Collection(ApiCollection.Name)]
public sealed class CalendarApiTests(ApiFixture fixture)
{
    private const string SkipReason = "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.";

    /// <summary>
    /// The calendar needs a signed-in photographer.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Calendar_WithoutAToken_IsUnauthorized()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var from = DateTimeOffset.UtcNow;

        var response = await client.SendAsync(HttpMethod.Get, RangeUrl(from, from.AddDays(7)), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The answer holds the bookings that touch the range, by the start of the session, and leaves out the ones outside it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Calendar_ReturnsTheBookingsOfTheRange_OrderedByStart()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var monday = NextMonday();
        var late = await CreateAsync(client, session.AccessToken, monday.AddDays(2).AddHours(15));
        var early = await CreateAsync(client, session.AccessToken, monday.AddHours(9));
        var outside = await CreateAsync(client, session.AccessToken, monday.AddDays(20));

        var response = await client.SendAsync(HttpMethod.Get, RangeUrl(monday, monday.AddDays(7)), session.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        Ids(json).ShouldBe([early, late]);
        Ids(json).ShouldNotContain(outside);
        json.GetProperty("truncated").GetBoolean().ShouldBeFalse();
        var first = json.GetProperty("items")[0];
        first.GetProperty("clientName").GetString().ShouldBe("María Pérez");
        first.GetProperty("status").GetString().ShouldBe("Tentative");
    }

    /// <summary>
    /// By default a cancelled booking is not in the answer; asking for the ended ones brings it back.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Calendar_HidesTheEndedBookingsUnlessAsked()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var monday = NextMonday();
        var kept = await CreateAsync(client, session.AccessToken, monday.AddHours(9));
        var cancelled = await CreateAsync(client, session.AccessToken, monday.AddDays(1).AddHours(9));
        (await client.SendAsync(HttpMethod.Post, $"/api/bookings/{cancelled}/cancel", session.AccessToken, new { })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var byDefault = await (await client.SendAsync(HttpMethod.Get, RangeUrl(monday, monday.AddDays(7)), session.AccessToken)).ReadJsonAsync();
        var withEnded = await (await client.SendAsync(HttpMethod.Get, RangeUrl(monday, monday.AddDays(7)) + "&includeEnded=true", session.AccessToken)).ReadJsonAsync();

        Ids(byDefault).ShouldBe([kept]);
        Ids(withEnded).ShouldBe([kept, cancelled]);
        withEnded.GetProperty("items")[1].GetProperty("status").GetString().ShouldBe("Cancelled");
    }

    /// <summary>
    /// A photographer never sees the bookings of another one in the calendar.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Calendar_NeverShowsTheBookingsOfAnotherPhotographer()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var other = await client.LoginAsync(await fixture.CreateUserAsync());
        var monday = NextMonday();
        var own = await CreateAsync(client, owner.AccessToken, monday.AddHours(9));
        await CreateAsync(client, other.AccessToken, monday.AddHours(9));

        var json = await (await client.SendAsync(HttpMethod.Get, RangeUrl(monday, monday.AddDays(7)), owner.AccessToken)).ReadJsonAsync();

        Ids(json).ShouldBe([own]);
    }

    /// <summary>
    /// A range that is missing, empty, inverted or longer than 62 days is a 400 with the stable code of a bad request.
    /// </summary>
    /// <param name="query">Query string sent.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("?from=2026-11-01T00:00:00Z")]
    [InlineData("?to=2026-11-08T00:00:00Z")]
    [InlineData("?from=2026-11-08T00:00:00Z&to=2026-11-01T00:00:00Z")]
    [InlineData("?from=2026-11-01T00:00:00Z&to=2026-11-01T00:00:00Z")]
    [InlineData("?from=2026-11-01T00:00:00Z&to=2027-01-03T00:00:00Z")]
    [InlineData("?from=not-a-date&to=2026-11-08T00:00:00Z")]
    public async Task Calendar_WithAnInvalidRange_IsABadRequest(string query)
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());

        var response = await client.SendAsync(HttpMethod.Get, "/api/bookings/calendar" + query, session.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadCodeAsync()).ShouldBe("request.invalid");
    }

    /// <summary>
    /// The longest accepted range, 62 days, works.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Calendar_AcceptsTheLongestRange()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var from = NextMonday();

        var response = await client.SendAsync(HttpMethod.Get, RangeUrl(from, from.AddDays(62)), session.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// The route of the calendar does not get in the way of reading a booking by its identifier.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Calendar_DoesNotShadowReadingABooking()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var id = await CreateAsync(client, session.AccessToken, NextMonday().AddHours(9));

        var response = await client.SendAsync(HttpMethod.Get, $"/api/bookings/{id}", session.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Builds the URL of a range, with the instants in ISO 8601 and encoded.
    /// </summary>
    /// <param name="from">Start of the range.</param>
    /// <param name="to">End of the range.</param>
    /// <returns>The path and query string.</returns>
    private static string RangeUrl(DateTimeOffset from, DateTimeOffset to) =>
        $"/api/bookings/calendar?from={Uri.EscapeDataString(from.ToString("O", CultureInfo.InvariantCulture))}&to={Uri.EscapeDataString(to.ToString("O", CultureInfo.InvariantCulture))}";

    /// <summary>
    /// Gets a midnight (UTC) well ahead of today, far from the instants other tests use, so a week of it is free.
    /// </summary>
    /// <returns>The instant.</returns>
    private static DateTimeOffset NextMonday()
    {
        var day = DateTimeOffset.UtcNow.Date.AddDays(60 + Random.Shared.Next(0, 200));
        return new DateTimeOffset(day, TimeSpan.Zero);
    }

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

    /// <summary>
    /// Reads the identifiers of the bookings of an answer, in order.
    /// </summary>
    /// <param name="json">The answer.</param>
    /// <returns>The identifiers.</returns>
    private static List<Guid> Ids(JsonElement json) => [.. json.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
}
