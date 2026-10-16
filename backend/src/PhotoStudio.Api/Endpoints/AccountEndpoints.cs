using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using PhotoStudio.Api.Auth;
using PhotoStudio.Api.RateLimiting;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Identity;
using PhotoStudio.Application.Identity.ChangePassword;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// HTTP body to change the password of the signed-in user.
/// </summary>
/// <param name="CurrentPassword">Password the user has now.</param>
/// <param name="NewPassword">Password the user wants: 8 to 128 characters.</param>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// Endpoints of the signed-in user's own account. They require an access token, unlike <see cref="AuthEndpoints"/>.
/// </summary>
public static class AccountEndpoints
{
    /// <summary>
    /// Maps the account endpoints under <c>/api/account</c>.
    /// </summary>
    /// <param name="endpoints">Route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/account").WithTags("Account").RequireAuthorization();

        group.MapPost("/password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy);

        return endpoints;
    }

    /// <summary>
    /// Changes the password. The other sessions of the user are closed and the response carries the session of this device.
    /// </summary>
    /// <param name="request">Current and new password.</param>
    /// <param name="user">The authenticated user.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the new session; 422 when the current password is wrong or the new one is not valid; 429 when the user is locked or the rate limit is hit.</returns>
    private static async Task<Ok<AuthSessionResponse>> ChangePasswordAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal user,
        ICommandHandler<ChangePasswordCommand, AuthSessionResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ChangePasswordCommand(user.GetPhotographerId(), request.CurrentPassword, request.NewPassword),
            cancellationToken));
}
