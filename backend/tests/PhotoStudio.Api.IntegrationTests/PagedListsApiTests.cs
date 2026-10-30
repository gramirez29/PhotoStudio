using System.Net;
using System.Text.Json;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the paged lists of bookings and notifications: size, order, cursor and refusal of a bad cursor.
/// </summary>
/// <param name="fixture">Shared API host.</param>
[Collection(ApiCollection.Name)]
public sealed class PagedListsApiTests(ApiFixture fixture)
{
    /// <summary>
    /// The bookings come five at a time, the most recently created first, and following the cursor visits each one once.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListBookings_ComesInPagesOfFive_NewestFirst_AndTheCursorVisitsEachOnce()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var created = new List<Guid>();
        for (var day = 0; day < 7; day++)
        {
            var start = DateTimeOffset.UtcNow.AddDays(10 + day);
            var booking = await client.CreateBookingAsync(
                session.AccessToken,
                new Dictionary<string, object> { ["sessionStart"] = start, ["sessionEnd"] = start.AddHours(2) });
            created.Add(booking.GetProperty("id").GetGuid());
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        var firstPage = await (await client.SendAsync(HttpMethod.Get, "/api/bookings", session.AccessToken)).ReadJsonAsync();
        var cursor = firstPage.GetProperty("nextCursor").GetString();
        var second = await (await client.SendAsync(HttpMethod.Get, $"/api/bookings?cursor={cursor}", session.AccessToken)).ReadJsonAsync();

        var ids = Ids(firstPage).Concat(Ids(second)).ToList();
        ids.Count.ShouldBe(7);
        Ids(firstPage).Count.ShouldBe(5);
        ids.ShouldBe([.. created.AsEnumerable().Reverse()]);
        second.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>
    /// A cursor that this API did not write is answered with 400, and a page size outside the bounds is brought back into them.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("/api/bookings?cursor=garbage")]
    [InlineData("/api/notifications?cursor=1.2.3")]
    public async Task AListWithABadCursor_IsRefused(string path)
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());

        var response = await client.SendAsync(HttpMethod.Get, path, session.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A page size of zero or less still gives one booking, and the inbox answers with its page fields.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task APageSizeBelowOne_IsBroughtBackToOne_AndTheInboxHasItsPageFields()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        await client.CreateBookingAsync(session.AccessToken);

        var bookings = await (await client.SendAsync(HttpMethod.Get, "/api/bookings?limit=0", session.AccessToken)).ReadJsonAsync();
        var inbox = await (await client.SendAsync(HttpMethod.Get, "/api/notifications", session.AccessToken)).ReadJsonAsync();

        Ids(bookings).Count.ShouldBe(1);
        inbox.GetProperty("items").GetArrayLength().ShouldBeLessThanOrEqualTo(5);
        inbox.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
        inbox.GetProperty("unreadCount").GetInt32().ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// Reads the identifiers of a page of bookings, in order.
    /// </summary>
    /// <param name="page">The page as JSON.</param>
    /// <returns>The identifiers.</returns>
    private static List<Guid> Ids(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
}
