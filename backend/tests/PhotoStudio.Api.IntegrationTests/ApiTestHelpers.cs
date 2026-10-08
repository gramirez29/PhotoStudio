using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// A signed-in session as the API returns it, reduced to what the tests need.
/// </summary>
/// <param name="AccessToken">Access token.</param>
/// <param name="RefreshToken">Refresh token secret.</param>
/// <param name="PhotographerId">Photographer (tenant) identifier.</param>
/// <param name="Username">Username of the user.</param>
public sealed record SessionTokens(string AccessToken, string RefreshToken, Guid PhotographerId, string Username);

/// <summary>
/// Helpers shared by the API tests to talk to the endpoints.
/// </summary>
internal static class ApiTestHelpers
{
    /// <summary>
    /// Signs in and returns the raw response.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="username">Username.</param>
    /// <param name="password">Password.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> LoginRawAsync(this HttpClient client, string username, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { username, password }, TestContext.Current.CancellationToken);

    /// <summary>
    /// Signs in with the right credentials and returns the session.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="user">User to sign in.</param>
    /// <returns>The session.</returns>
    public static async Task<SessionTokens> LoginAsync(this HttpClient client, TestUser user)
    {
        var response = await client.LoginRawAsync(user.Username, user.Password);
        response.EnsureSuccessStatusCode();
        return await response.ReadSessionAsync();
    }

    /// <summary>
    /// Creates an account through the registration endpoint and returns the raw response.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="username">Username.</param>
    /// <param name="password">Password.</param>
    /// <param name="name">Display name.</param>
    /// <param name="phone">Phone number.</param>
    /// <param name="email">Email; defaults to one derived from the username.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> RegisterRawAsync(
        this HttpClient client,
        string username,
        string password = "a long enough password",
        string name = "Ana Pérez",
        string phone = "+506 7018-9220",
        string? email = null) =>
        client.PostAsJsonAsync(
            "/api/auth/register",
            new { username, email = email ?? $"{username}@example.com", password, name, phone },
            TestContext.Current.CancellationToken);

    /// <summary>
    /// Refreshes a session and returns the raw response.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="refreshToken">Refresh token secret.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> RefreshRawAsync(this HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken }, TestContext.Current.CancellationToken);

    /// <summary>
    /// Reads the tokens of a session response.
    /// </summary>
    /// <param name="response">Successful login or refresh response.</param>
    /// <returns>The session.</returns>
    public static async Task<SessionTokens> ReadSessionAsync(this HttpResponseMessage response)
    {
        var json = await response.ReadJsonAsync();
        return new SessionTokens(
            json.GetProperty("accessToken").GetString()!,
            json.GetProperty("refreshToken").GetString()!,
            json.GetProperty("photographerId").GetGuid(),
            json.GetProperty("username").GetString()!);
    }

    /// <summary>
    /// Reads the body of a response as JSON.
    /// </summary>
    /// <param name="response">Response with a JSON body.</param>
    /// <returns>The root element.</returns>
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>
    /// Reads the stable <c>code</c> of a problem details response.
    /// </summary>
    /// <param name="response">Error response.</param>
    /// <returns>The code.</returns>
    public static async Task<string?> ReadCodeAsync(this HttpResponseMessage response)
    {
        var json = await response.ReadJsonAsync();
        return json.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    /// <summary>
    /// Sends a request with an access token.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Path of the endpoint.</param>
    /// <param name="accessToken">Access token, or <see langword="null"/> to send none.</param>
    /// <param name="body">Optional JSON body.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> SendAsync(this HttpClient client, HttpMethod method, string path, string? accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Creates a booking for the signed-in photographer.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="accessToken">Access token of the photographer.</param>
    /// <param name="extra">Extra properties for the body, for example a forged <c>photographerId</c>.</param>
    /// <returns>The created booking as JSON.</returns>
    public static async Task<JsonElement> CreateBookingAsync(this HttpClient client, string accessToken, IReadOnlyDictionary<string, object>? extra = null)
    {
        var start = DateTimeOffset.UtcNow.AddDays(10);
        var body = new Dictionary<string, object>
        {
            ["clientName"] = "María Pérez",
            ["clientPhone"] = "+50670189220",
            ["packageName"] = "Retrato Familiar",
            ["packagePrice"] = 100000,
            ["currency"] = "CRC",
            ["sessionStart"] = start,
            ["sessionEnd"] = start.AddHours(2),
        };
        if (extra is not null)
        {
            foreach (var (key, value) in extra)
            {
                body[key] = value;
            }
        }

        var response = await client.SendAsync(HttpMethod.Post, "/api/bookings", accessToken, body);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created);
        return await response.ReadJsonAsync();
    }
}
