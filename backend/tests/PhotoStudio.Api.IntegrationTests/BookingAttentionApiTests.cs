using System.Net;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of <c>GET /api/bookings/attention</c>: what is waiting for the photographer, for the home screen.
/// </summary>
/// <param name="fixture">Shared API host.</param>
[Collection(ApiCollection.Name)]
public sealed class BookingAttentionApiTests(ApiFixture fixture)
{
    private const string SkipReason = "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.";

    /// <summary>
    /// The summary needs a signed-in photographer.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Attention_WithoutAToken_IsUnauthorized()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);

        var response = await fixture.CreateClient().SendAsync(HttpMethod.Get, "/api/bookings/attention", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A new photographer has nothing waiting.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Attention_ForANewPhotographer_IsZeroForBoth()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());

        var response = await client.SendAsync(HttpMethod.Get, "/api/bookings/attention", session.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("pendingProofs").GetInt32().ShouldBe(0);
        json.GetProperty("expiringSoon").GetInt32().ShouldBe(0);
    }

    /// <summary>
    /// A booking whose session starts within a day has a hold that ends then, so it counts as about to expire; one far away does not,
    /// and neither does one of another photographer.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Attention_CountsTheTentativeBookingsAboutToExpire_OfTheOwnPhotographerOnly()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var other = await client.LoginAsync(await fixture.CreateUserAsync());
        var soon = DateTimeOffset.UtcNow.AddHours(6);
        var far = DateTimeOffset.UtcNow.AddDays(90 + Random.Shared.Next(0, 100));
        await CreateAsync(client, owner.AccessToken, soon);
        await CreateAsync(client, owner.AccessToken, far);
        await CreateAsync(client, other.AccessToken, soon);

        var json = await (await client.SendAsync(HttpMethod.Get, "/api/bookings/attention", owner.AccessToken)).ReadJsonAsync();

        json.GetProperty("expiringSoon").GetInt32().ShouldBe(1);
        json.GetProperty("pendingProofs").GetInt32().ShouldBe(0);
    }

    /// <summary>
    /// Creates a tentative booking of two hours starting at the given instant.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="accessToken">Access token of the photographer.</param>
    /// <param name="start">Session start.</param>
    /// <returns>A task that completes when the booking is created.</returns>
    private static async Task CreateAsync(HttpClient client, string accessToken, DateTimeOffset start) =>
        await client.CreateBookingAsync(
            accessToken,
            new Dictionary<string, object> { ["sessionStart"] = start, ["sessionEnd"] = start.AddHours(2) });
}
