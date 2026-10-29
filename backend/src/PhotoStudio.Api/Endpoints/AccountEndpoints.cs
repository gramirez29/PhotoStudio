using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using PhotoStudio.Api.Auth;
using PhotoStudio.Api.RateLimiting;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Identity;
using PhotoStudio.Application.Identity.ChangePassword;
using PhotoStudio.Application.Identity.ResendEmailVerification;
using PhotoStudio.Application.Identity.VerifyEmail;
using PhotoStudio.Infrastructure.Identity;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// HTTP body to change the password of the signed-in user.
/// </summary>
/// <param name="CurrentPassword">Password the user has now.</param>
/// <param name="NewPassword">Password the user wants: 8 to 128 characters.</param>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// HTTP body to prove the email with the code that was sent there.
/// </summary>
/// <param name="Code">The six digits the user typed.</param>
public sealed record VerifyEmailRequest(string Code);

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

        // The only endpoints a user who has not verified their email can use: the ones that verify it. Everything else of the API,
        // this group included, refuses that user.
        var email = endpoints.MapGroup("/api/account/email").WithTags("Account").RequireAuthorization(PhotographerAuthenticationExtensions.AnyUserPolicy);

        email.MapPost("/verify", VerifyEmailAsync).WithName("VerifyEmail").RequireRateLimiting(RateLimitingExtensions.AuthPolicy);
        email.MapPost("/resend", ResendEmailVerificationAsync).WithName("ResendEmailVerification").RequireRateLimiting(RateLimitingExtensions.AuthPolicy);

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

    /// <summary>
    /// Proves the email of the signed-in user with the code that was sent there.
    /// </summary>
    /// <param name="request">Code typed by the user.</param>
    /// <param name="user">Authenticated user.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with a new session whose token lets the user into the app; 422 <c>auth.invalid_email_code</c> when the code is wrong, expired or spent.</returns>
    private static async Task<Ok<AuthSessionResponse>> VerifyEmailAsync(
        VerifyEmailRequest request,
        ClaimsPrincipal user,
        ICommandHandler<VerifyEmailCommand, AuthSessionResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new VerifyEmailCommand(user.GetPhotographerId(), request.Code), cancellationToken));

    /// <summary>
    /// Sends a new code to the email of the signed-in user.
    /// </summary>
    /// <param name="user">Authenticated user.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>202 with where the code went (masked); 409 when the email is already verified or the account has none; 429 <c>auth.email_code_rate_limited</c> (with <c>Retry-After</c>); 503 <c>email.delivery_failed</c>.</returns>
    private static async Task<Accepted<EmailVerificationSentResponse>> ResendEmailVerificationAsync(
        ClaimsPrincipal user,
        ICommandHandler<ResendEmailVerificationCommand, EmailVerificationSentResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Accepted(
            (string?)null,
            await handler.HandleAsync(new ResendEmailVerificationCommand(user.GetPhotographerId()), cancellationToken));
}
