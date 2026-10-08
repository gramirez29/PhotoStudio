using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// Tests that the API only accepts well-formed access tokens signed with its own key, and that the public endpoints stay
/// public while everything else requires a signed-in photographer.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class AccessTokenValidationApiTests(ApiFixture fixture)
{
    /// <summary>
    /// Every protected endpoint refuses a request without a token and asks for a bearer token.
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Endpoint path.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("GET", "/api/bookings")]
    [InlineData("POST", "/api/bookings")]
    [InlineData("GET", "/api/bookings/0199a1b2-0000-7000-8000-000000000001")]
    [InlineData("POST", "/api/bookings/0199a1b2-0000-7000-8000-000000000001/cancel")]
    [InlineData("POST", "/api/maintenance/run")]
    public async Task ProtectedEndpoints_WithoutAToken_Return401(string method, string path)
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");

        var response = await fixture.CreateClient().SendAsync(new HttpMethod(method), path, null, method == "POST" ? new { } : null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldStartWith("Bearer");
    }

    /// <summary>
    /// Health checks and the OpenAPI document stay public, because Railway and the type generator call them without credentials.
    /// </summary>
    /// <param name="path">Endpoint path.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/openapi/v1.json")]
    public async Task PublicEndpoints_AnswerWithoutAToken(string path)
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");

        var response = await fixture.CreateClient().SendAsync(HttpMethod.Get, path, null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// A valid token reaches the maintenance endpoint.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task MaintenanceEndpoint_WithAValidToken_Answers200()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateAccountAsync());

        var response = await client.SendAsync(HttpMethod.Post, "/api/maintenance/run", session.AccessToken);

        // The shared maintenance limiter allows one call per minute for the whole API; another test may have used it.
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// A token whose payload was changed after signing is refused.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ATamperedToken_IsRefused()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateAccountAsync());
        var parts = session.AccessToken.Split('.');
        var forgedPayload = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes($$"""{"sub":"{{Guid.CreateVersion7()}}","iss":"{{ApiFixture.Issuer}}","aud":"{{ApiFixture.Audience}}","exp":{{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}"""));
        var tampered = $"{parts[0]}.{forgedPayload}.{parts[2]}";

        var response = await client.SendAsync(HttpMethod.Get, "/api/bookings", tampered);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A token signed with another key is refused, even when everything else about it is right.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ATokenSignedWithAnotherKey_IsRefused()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");

        var token = CreateToken(signingKey: "another-key-another-key-another-key-0123456789");

        (await fixture.CreateClient().SendAsync(HttpMethod.Get, "/api/bookings", token)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// An expired token is refused.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AnExpiredToken_IsRefused()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");

        var token = CreateToken(issuedAt: DateTime.UtcNow.AddHours(-2), expires: DateTime.UtcNow.AddHours(-1));

        (await fixture.CreateClient().SendAsync(HttpMethod.Get, "/api/bookings", token)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A token for another audience or from another issuer is refused.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ATokenForAnotherAudienceOrIssuer_IsRefused()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();

        (await client.SendAsync(HttpMethod.Get, "/api/bookings", CreateToken(audience: "someone-else"))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.SendAsync(HttpMethod.Get, "/api/bookings", CreateToken(issuer: "someone-else"))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The control case: a token built by the test with the right key, issuer, audience and lifetime is accepted, so the
    /// refusals above are caused by the property they change and not by a mistake in the helper.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AWellFormedToken_IsAccepted()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");

        var token = CreateToken();

        (await fixture.CreateClient().SendAsync(HttpMethod.Get, "/api/bookings", token)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// An unsigned token (<c>alg: none</c>) is refused.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AnUnsignedToken_IsRefused()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var header = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["sub"] = Guid.CreateVersion7().ToString(),
            ["iss"] = ApiFixture.Issuer,
            ["aud"] = ApiFixture.Audience,
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
        }));

        var response = await fixture.CreateClient().SendAsync(HttpMethod.Get, "/api/bookings", $"{header}.{payload}.");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Builds an access token as the API would, with any property replaced so a test can break exactly one thing.
    /// </summary>
    /// <param name="signingKey">Key to sign with; defaults to the key of the API.</param>
    /// <param name="issuer">Issuer claim.</param>
    /// <param name="audience">Audience claim.</param>
    /// <param name="issuedAt">Issue instant.</param>
    /// <param name="expires">Expiry instant.</param>
    /// <returns>The signed token.</returns>
    private static string CreateToken(
        string signingKey = ApiFixture.SigningKey,
        string issuer = ApiFixture.Issuer,
        string audience = ApiFixture.Audience,
        DateTime? issuedAt = null,
        DateTime? expires = null) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = issuedAt ?? DateTime.UtcNow,
            NotBefore = issuedAt ?? DateTime.UtcNow,
            Expires = expires ?? DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, Guid.CreateVersion7().ToString())]),
        });
}
