using Microsoft.AspNetCore.Http.HttpResults;
using PhotoStudio.Api.RateLimiting;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Identity;
using PhotoStudio.Application.Identity.Login;
using PhotoStudio.Application.Identity.Logout;
using PhotoStudio.Application.Identity.RefreshSession;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// Endpoints that sign the photographer in and out. They are the only ones, besides health and OpenAPI, that do not require
/// an access token, so they are rate limited.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// Maps the authentication endpoints under <c>/api/auth</c>.
    /// </summary>
    /// <param name="endpoints">Route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/auth")
            .WithTags("Authentication")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy);

        group.MapPost("/login", LoginAsync).WithName("Login");
        group.MapPost("/refresh", RefreshAsync).WithName("RefreshSession");
        group.MapPost("/logout", LogoutAsync).WithName("Logout");

        return endpoints;
    }

    /// <summary>
    /// Signs in with email and password.
    /// </summary>
    /// <param name="request">Credentials.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the session; 401 when the credentials are not accepted; 429 when the account is locked or the rate limit is hit.</returns>
    private static async Task<Ok<AuthSessionResponse>> LoginAsync(
        LoginRequest request,
        ICommandHandler<LoginCommand, AuthSessionResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToCommand(), cancellationToken));

    /// <summary>
    /// Exchanges a refresh token for a new session. The refresh token is single-use: the response carries its replacement.
    /// </summary>
    /// <param name="request">Refresh token.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the new session; 401 when the token is unknown, expired or already used.</returns>
    private static async Task<Ok<AuthSessionResponse>> RefreshAsync(
        RefreshRequest request,
        ICommandHandler<RefreshSessionCommand, AuthSessionResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToCommand(), cancellationToken));

    /// <summary>
    /// Closes the session that owns the refresh token. It answers 204 whether or not the token was known, so it never
    /// reveals which tokens exist.
    /// </summary>
    /// <param name="request">Refresh token of the session to close.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>204 with no content.</returns>
    private static async Task<NoContent> LogoutAsync(
        LogoutRequest request,
        ICommandHandler<LogoutCommand, LogoutResponse> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request.ToCommand(), cancellationToken);
        return TypedResults.NoContent();
    }
}
