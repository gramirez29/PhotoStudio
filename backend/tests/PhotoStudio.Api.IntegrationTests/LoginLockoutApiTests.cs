using System.Net;
using System.Net.Http.Json;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the limit on password guesses, which is kept per account <b>and</b> network address: somebody who only knows
/// a username cannot lock its owner out, and several addresses together can still lock the account. The API reads the address from
/// <c>X-Forwarded-For</c>, as it does behind the proxy.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class LoginLockoutApiTests(ApiFixture fixture)
{
    private const string SkipReason = "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.";

    /// <summary>
    /// A stranger who fails five times is refused, even with the right password, and the owner signing in from another address is
    /// not affected at all.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AStrangerExhaustingTheirAddress_DoesNotLockTheOwnerOut()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var account = await fixture.CreateUserAsync();
        var client = fixture.CreateClient();
        var strangerAddress = NewAddress();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await LoginFromAsync(client, account.Username, "wrong", strangerAddress)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var stranger = await LoginFromAsync(client, account.Username, account.Password, strangerAddress);
        var owner = await LoginFromAsync(client, account.Username, account.Password, NewAddress());

        stranger.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await stranger.ReadCodeAsync()).ShouldBe("auth.account_locked");
        owner.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Enough failures from enough different addresses lock the account for every address, the owner's included: this is what a
    /// single address cannot do.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ManyAddressesTogether_LockTheAccountForEveryAddress()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var account = await fixture.CreateUserAsync();
        var client = fixture.CreateClient();
        foreach (var address in Enumerable.Range(0, 4).Select(_ => NewAddress()))
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                (await LoginFromAsync(client, account.Username, "wrong", address)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            }
        }

        var owner = await LoginFromAsync(client, account.Username, account.Password, NewAddress());

        owner.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await owner.ReadCodeAsync()).ShouldBe("auth.account_locked");
    }

    /// <summary>
    /// The attempts an address makes while it is blocked are refused without being counted, so one address can never reach the limit
    /// of the whole account by insisting.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ABlockedAddressInsisting_NeverLocksTheAccount()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var account = await fixture.CreateUserAsync();
        var client = fixture.CreateClient();
        var strangerAddress = NewAddress();
        for (var attempt = 0; attempt < 8; attempt++)
        {
            await LoginFromAsync(client, account.Username, "wrong", strangerAddress);
        }

        (await LoginFromAsync(client, account.Username, account.Password, NewAddress())).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Addresses of the same IPv6 network share their failures, so a guesser cannot get a fresh count by changing the end of the address.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Ipv6AddressesOfTheSameNetwork_ShareTheirFailures()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var account = await fixture.CreateUserAsync();
        var client = fixture.CreateClient();
        var network = Random.Shared.Next(1, 0xFFFF);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await LoginFromAsync(client, account.Username, "wrong", $"2001:db8:{network:x}:2::{attempt}");
        }

        var sameNetwork = await LoginFromAsync(client, account.Username, account.Password, $"2001:db8:{network:x}:2:aaaa::1");
        var otherNetwork = await LoginFromAsync(client, account.Username, account.Password, $"2001:db8:{network:x}:3::1");

        sameNetwork.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        otherNetwork.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// A good login forgets the failures of its address: failures that were not followed by a block do not add up with later ones.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ASuccess_ForgetsTheFailuresOfItsAddress()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var account = await fixture.CreateUserAsync();
        var client = fixture.CreateClient();
        var address = NewAddress();
        for (var round = 0; round < 2; round++)
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                await LoginFromAsync(client, account.Username, "wrong", address);
            }

            (await LoginFromAsync(client, account.Username, account.Password, address)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    /// <summary>
    /// Wrong passwords tried through the change of password count too, per address, so the endpoint cannot be used to guess the
    /// current password.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ChangePassword_WithWrongCurrentPasswords_BlocksTheAddress()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var account = await fixture.CreateUserAsync();
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(account);
        var strangerAddress = NewAddress();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await ChangePasswordFromAsync(client, session.AccessToken, "wrong", strangerAddress)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        }

        var blocked = await ChangePasswordFromAsync(client, session.AccessToken, account.Password, strangerAddress);

        blocked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await LoginFromAsync(client, account.Username, account.Password, NewAddress())).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Builds an address nobody else in the test run uses. Each address has its own limit of calls per minute, so tests that share one
    /// would be refused by that limit instead of by the lockout they are about.
    /// </summary>
    /// <returns>An IPv4 address of the range reserved for tests.</returns>
    private static string NewAddress()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        return $"198.{18 + (bytes[0] % 2)}.{bytes[1]}.{(bytes[2] % 253) + 1}";
    }

    /// <summary>
    /// Signs in as a client that reaches the API from the given address.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="username">Username.</param>
    /// <param name="password">Password.</param>
    /// <param name="address">Address the proxy reports.</param>
    /// <returns>The response.</returns>
    private static async Task<HttpResponseMessage> LoginFromAsync(HttpClient client, string username, string password, string address)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { username, password }) };
        request.Headers.Add("X-Forwarded-For", address);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Asks to change the password as a client that reaches the API from the given address.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="accessToken">Access token of the user.</param>
    /// <param name="currentPassword">Current password to send.</param>
    /// <param name="address">Address the proxy reports.</param>
    /// <returns>The response.</returns>
    private static async Task<HttpResponseMessage> ChangePasswordFromAsync(HttpClient client, string accessToken, string currentPassword, string address)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/account/password")
        {
            Content = JsonContent.Create(new { currentPassword, newPassword = "a brand new password" }),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-Forwarded-For", address);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
