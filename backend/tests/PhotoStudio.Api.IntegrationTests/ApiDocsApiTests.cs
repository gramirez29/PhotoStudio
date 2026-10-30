using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PhotoStudio.Api.Documentation;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// Tests of the API documentation: Swagger UI and the OpenAPI document are served where they are turned on, describe how the
/// endpoints are secured, and do not exist where they are turned off.
/// </summary>
/// <param name="fixture">Shared API host, which runs in Development.</param>
[Collection(ApiCollection.Name)]
public sealed class ApiDocsApiTests(ApiFixture fixture)
{
    /// <summary>
    /// In Development the page to try the endpoints is served, from the API itself and without scripts from another site.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task InDevelopment_SwaggerUiIsServed_WithoutExternalScripts()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();

        var page = await client.SendAsync(HttpMethod.Get, "/swagger/index.html", null);
        var body = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        body.ShouldContain("swagger-ui");
        body.ShouldNotContain("src=\"http");
        body.ShouldNotContain("href=\"http");
        (await client.SendAsync(HttpMethod.Get, "/swagger/swagger-ui-bundle.js", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// The document declares the Bearer token and asks for it on every endpoint that needs a signed-in user, and on none of the public ones.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheDocument_DescribesTheBearerTokenOnlyWhereItIsRequired()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");

        var document = await (await fixture.CreateClient().SendAsync(HttpMethod.Get, ApiDocs.DocumentPath, null)).ReadJsonAsync();

        document.GetProperty("info").GetProperty("title").GetString().ShouldBe("PhotoSense Studio API");
        var scheme = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        scheme.GetProperty("type").GetString().ShouldBe("http");
        scheme.GetProperty("scheme").GetString().ShouldBe("bearer");
        scheme.GetProperty("bearerFormat").GetString().ShouldBe("JWT");
        var paths = document.GetProperty("paths");
        RequiresToken(paths, "/api/bookings", "get").ShouldBeTrue();
        RequiresToken(paths, "/api/galleries", "get").ShouldBeTrue();
        RequiresToken(paths, "/api/account/password", "post").ShouldBeTrue();
        RequiresToken(paths, "/api/auth/login", "post").ShouldBeFalse();
        RequiresToken(paths, "/api/auth/register", "post").ShouldBeFalse();
    }

    /// <summary>
    /// With the variable set to false the documentation does not exist, even in Development, and the rest of the API is untouched.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task WhenTurnedOff_TheRoutesDoNotExist_AndTheApiKeepsWorking()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await using var factory = fixture.CreateFactory(new Dictionary<string, string?> { [ApiDocs.EnabledVariable] = "false" });
        var client = fixture.CreateClient(factory);

        // The routes do not exist, so they answer exactly like any other path that does not: the API asks an anonymous caller for a session.
        var unknown = (await client.SendAsync(HttpMethod.Get, "/no-such-path", null)).StatusCode;
        unknown.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.SendAsync(HttpMethod.Get, ApiDocs.DocumentPath, null)).StatusCode.ShouldBe(unknown);
        (await client.SendAsync(HttpMethod.Get, "/swagger/index.html", null)).StatusCode.ShouldBe(unknown);
        (await client.SendAsync(HttpMethod.Get, "/swagger/swagger-ui-bundle.js", null)).StatusCode.ShouldBe(unknown);
        (await client.SendAsync(HttpMethod.Get, "/health/live", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.SendAsync(HttpMethod.Get, "/api/bookings", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Decides who gets the documentation: the variable wins when it says "true" or "false"; without it, only Development; any
    /// other text is off, so a typo in a production setting never opens it.
    /// </summary>
    /// <param name="environment">Name of the environment.</param>
    /// <param name="variable">Value of the variable, if set.</param>
    /// <param name="expected">Whether the documentation is on.</param>
    [Theory]
    [InlineData("Development", null, true)]
    [InlineData("Development", "", true)]
    [InlineData("Development", "false", false)]
    [InlineData("Development", "FALSE", false)]
    [InlineData("Production", null, false)]
    [InlineData("Production", "", false)]
    [InlineData("Production", "true", true)]
    [InlineData("Production", " TRUE ", true)]
    [InlineData("Production", "false", false)]
    [InlineData("Production", "yes", false)]
    [InlineData("Production", "1", false)]
    [InlineData("Production", "treu", false)]
    [InlineData("Development", "treu", false)]
    public void IsEnabled_FollowsTheVariableAndOtherwiseDevelopmentOnly(string environment, string? variable, bool expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [ApiDocs.EnabledVariable] = variable }).Build();

        ApiDocs.IsEnabled(new FakeEnvironment(environment), configuration).ShouldBe(expected);
    }

    /// <summary>
    /// Says whether an operation of the document asks for the Bearer token.
    /// </summary>
    /// <param name="paths">The <c>paths</c> object of the document.</param>
    /// <param name="path">Path of the endpoint.</param>
    /// <param name="method">HTTP method, in lowercase.</param>
    /// <returns><see langword="true"/> when the operation lists the Bearer scheme as a requirement.</returns>
    private static bool RequiresToken(JsonElement paths, string path, string method) =>
        paths.GetProperty(path).GetProperty(method).TryGetProperty("security", out var security)
        && security.EnumerateArray().Any(requirement => requirement.TryGetProperty("Bearer", out _));

    /// <summary>
    /// A host environment with the name a test gives it.
    /// </summary>
    /// <param name="environmentName">Name of the environment.</param>
    private sealed class FakeEnvironment(string environmentName) : IHostEnvironment
    {
        /// <inheritdoc />
        public string EnvironmentName { get; set; } = environmentName;

        /// <inheritdoc />
        public string ApplicationName { get; set; } = "PhotoStudio.Api";

        /// <inheritdoc />
        public string ContentRootPath { get; set; } = string.Empty;

        /// <inheritdoc />
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
