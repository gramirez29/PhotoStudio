using System.Net;
using System.Net.Http.Json;
using PhotoStudio.Api.Cors;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// Tests of the cross-origin policy of the portal: the portal origin can call the portal endpoints, in every kind of answer,
/// and no other origin can; the photographer API is not opened to browsers at all.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class PortalCorsTests(ApiFixture fixture)
{
    private const string SkipReason = "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.";
    private const string PortalOrigin = "https://portal.example.test";

    /// <summary>
    /// The preflight of the portal origin is answered without a token and lists what the portal needs.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Preflight_FromThePortalOrigin_IsAllowed()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);

        var response = await PreflightAsync($"/public/bookings/{Guid.NewGuid():N}/payments", PortalOrigin, "POST", "authorization,content-type");

        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Headers.GetValues("Access-Control-Allow-Origin").Single().ShouldBe(PortalOrigin);
        response.Headers.GetValues("Access-Control-Allow-Methods").Single().ShouldContain("POST");
        response.Headers.GetValues("Access-Control-Allow-Headers").Single().ShouldContain("Authorization", Case.Insensitive);
        response.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse();
    }

    /// <summary>
    /// The development server of the portal is allowed while the API runs in Development.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Preflight_FromTheLocalDevelopmentServer_IsAllowedInDevelopment()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);

        var response = await PreflightAsync($"/public/bookings/{Guid.NewGuid():N}/code", "http://localhost:5173", "POST", "content-type");

        response.Headers.GetValues("Access-Control-Allow-Origin").Single().ShouldBe("http://localhost:5173");
    }

    /// <summary>
    /// Any other origin gets no permission headers, so the browser blocks the call.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Preflight_FromAnotherOrigin_GetsNoPermission()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);

        var response = await PreflightAsync($"/public/bookings/{Guid.NewGuid():N}/code", "https://evil.example", "POST", "content-type");

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    /// <summary>
    /// The photographer API is not open to browsers: even the portal origin gets no permission there.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Preflight_ToThePhotographerApi_GetsNoPermission()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);

        var response = await PreflightAsync("/api/bookings", PortalOrigin, "GET", "authorization");

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    /// <summary>
    /// Error answers carry the permission headers too. The portal's errors are all exceptions turned into problem details, and
    /// without the header the browser would hide them behind a generic network failure.
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Path of the portal endpoint, which answers with an error.</param>
    /// <param name="expected">Status the endpoint answers with.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("GET", "", HttpStatusCode.NotFound)]
    [InlineData("POST", "/code/verify", HttpStatusCode.NotFound)]
    [InlineData("POST", "/payments", HttpStatusCode.Unauthorized)]
    public async Task ErrorAnswers_CarryThePermissionHeaders(string method, string path, HttpStatusCode expected)
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/public/bookings/{Guid.NewGuid():N}{path}");
        request.Headers.Add("Origin", PortalOrigin);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { code = "123456", amount = 1, idempotencyKey = "k" });
        }

        var response = await fixture.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expected);
        response.Headers.GetValues("Access-Control-Allow-Origin").Single().ShouldBe(PortalOrigin);
    }

    /// <summary>
    /// A rate-limited answer lets the page read <c>Retry-After</c>, which it needs for the resend countdown.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TooManyCodes_ExposesRetryAfterToThePage()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var email = $"c{Guid.NewGuid():N}@example.com";
        var booking = await client.CreateBookingAsync(session.AccessToken, new Dictionary<string, object> { ["clientEmail"] = email });
        var link = await (await client.SendAsync(HttpMethod.Post, $"/api/bookings/{booking.GetProperty("id").GetGuid()}/link", session.AccessToken)).ReadJsonAsync();
        var token = link.GetProperty("token").GetString()!;
        await client.SendAsync(HttpMethod.Post, $"/public/bookings/{token}/code", null);

        using var again = new HttpRequestMessage(HttpMethod.Post, $"/public/bookings/{token}/code");
        again.Headers.Add("Origin", PortalOrigin);
        var response = await client.SendAsync(again, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        response.Headers.GetValues("Access-Control-Expose-Headers").Single().ShouldContain("Retry-After", Case.Insensitive);
        response.Headers.Contains("Retry-After").ShouldBeTrue();
    }

    /// <summary>
    /// The allowed origins are the portal's, the extra ones, and the local server only in Development.
    /// </summary>
    [Fact]
    public void ResolveOrigins_CombinesThePortalTheExtrasAndTheDevelopmentServer()
    {
        var production = PortalCorsExtensions.ResolveOrigins("https://book.example.test/some/path", ["https://preview.example.test"], false);
        var development = PortalCorsExtensions.ResolveOrigins("https://book.example.test", [], true);

        production.ShouldBe(["https://book.example.test", "https://preview.example.test"]);
        development.ShouldBe(["https://book.example.test", "http://localhost:5173", "http://127.0.0.1:5173"]);
    }

    /// <summary>
    /// An extra origin that repeats the portal's is listed once.
    /// </summary>
    [Fact]
    public void ResolveOrigins_DoesNotRepeatAnOrigin()
    {
        PortalCorsExtensions.ResolveOrigins("https://book.example.test", ["https://BOOK.example.test"], false).Length.ShouldBe(1);
    }

    /// <summary>
    /// The extra origins are read from a comma-separated list, trimmed and without trailing slashes.
    /// </summary>
    [Fact]
    public void ParseExtraOrigins_ReadsAList()
    {
        PortalCorsExtensions.ParseExtraOrigins(" https://a.example.test/ , http://localhost:3000 ").ShouldBe(["https://a.example.test", "http://localhost:3000"]);
        PortalCorsExtensions.ParseExtraOrigins(null).ShouldBeEmpty();
        PortalCorsExtensions.ParseExtraOrigins("  ").ShouldBeEmpty();
    }

    /// <summary>
    /// A wildcard, a path or a scheme other than http(s) stops the API at startup instead of being ignored.
    /// </summary>
    /// <param name="value">Invalid value of the variable.</param>
    [Theory]
    [InlineData("*")]
    [InlineData("https://a.example.test/path")]
    [InlineData("ftp://a.example.test")]
    [InlineData("a.example.test")]
    public void ParseExtraOrigins_RejectsWhatIsNotAnOrigin(string value)
    {
        Should.Throw<InvalidOperationException>(() => PortalCorsExtensions.ParseExtraOrigins(value));
    }

    /// <summary>
    /// Sends the preflight request a browser sends before a cross-origin call.
    /// </summary>
    /// <param name="path">Path the page wants to call.</param>
    /// <param name="origin">Origin of the page.</param>
    /// <param name="method">Method the page wants to use.</param>
    /// <param name="headers">Request headers the page wants to send, comma separated.</param>
    /// <returns>The response.</returns>
    private async Task<HttpResponseMessage> PreflightAsync(string path, string origin, string method, string headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", headers);
        return await fixture.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);
    }
}
