using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using PhotoStudio.Api.Auth;
using PhotoStudio.Api.RateLimiting;
using PhotoStudio.Api.Studio;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Galleries;
using PhotoStudio.Application.Galleries.GetGallery;
using PhotoStudio.Application.Galleries.ListGalleries;
using PhotoStudio.Application.Identity.Studio;
using PhotoStudio.Application.Pagination;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// Body to start a web sign-in.
/// </summary>
/// <param name="Username">Username.</param>
/// <param name="Password">Password.</param>
public sealed record StudioLoginRequest(string? Username, string? Password);

/// <summary>
/// Body to finish a web sign-in with the code from the email.
/// </summary>
/// <param name="Code">The six digits.</param>
/// <param name="RememberDevice">Whether to skip the code in this browser for 30 days.</param>
public sealed record StudioVerifyRequest(string? Code, bool RememberDevice);

/// <summary>
/// Body to sign out of the web area.
/// </summary>
/// <param name="ForgetDevice">Whether to also forget this browser, so the next sign-in asks for the code again.</param>
public sealed record StudioLogoutRequest(bool ForgetDevice);

/// <summary>
/// The user of a web session, as the page sees them.
/// </summary>
/// <param name="User">Who is signed in.</param>
public sealed record StudioSessionResponse(StudioUserSummary User);

/// <summary>
/// The answer to a password that was right and a browser that is remembered, or to a correct code: the session cookie is set.
/// </summary>
/// <param name="Status">Always <c>SignedIn</c>.</param>
/// <param name="User">Who signed in.</param>
public sealed record StudioSignedInResponse(string Status, StudioUserSummary User);

/// <summary>
/// The answer to a password that was right in a browser that is not remembered: a code went to the email and the challenge cookie is set.
/// </summary>
/// <param name="Status">Always <c>CodeRequired</c>.</param>
/// <param name="MaskedEmail">Where the code went, with the middle hidden.</param>
/// <param name="ExpiresAt">Instant the code stops being accepted (UTC).</param>
public sealed record StudioCodeRequiredResponse(string Status, string MaskedEmail, DateTimeOffset ExpiresAt);

/// <summary>
/// The web area of the photographer: a sign-in with password and a code by email, whose session is a cookie, and the endpoints that
/// area may use. The area sees less than the app on purpose: only the galleries. Every route is under <c>/api/web</c>, answers with
/// <c>Cache-Control: no-store</c>, and the ones that change something must come from the site of the area (see
/// <see cref="StudioWebExtensions.UseStudioWebGuard"/>).
/// </summary>
public static class StudioEndpoints
{
    /// <summary>
    /// Maps the web area under <c>/api/web</c>.
    /// </summary>
    /// <param name="endpoints">Route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapStudioEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var auth = endpoints.MapGroup("/api/web/auth").WithTags("Web area").RequireCors(StudioWebSettings.CorsPolicyName);
        auth.MapPost("/login", LoginAsync).WithName("StudioLogin").AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.AuthPolicy);
        auth.MapPost("/code/verify", VerifyAsync).WithName("StudioVerifyCode").AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.AuthPolicy);
        auth.MapPost("/code/resend", ResendAsync).WithName("StudioResendCode").AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.AuthPolicy);
        auth.MapPost("/logout", LogoutAsync).WithName("StudioLogout").AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.AuthPolicy);
        auth.MapGet("/session", GetSession).WithName("StudioSession").RequireAuthorization(StudioWebSettings.Policy);

        var galleries = endpoints.MapGroup("/api/web/galleries").WithTags("Web area").RequireCors(StudioWebSettings.CorsPolicyName).RequireAuthorization(StudioWebSettings.Policy);
        galleries.MapGet("/", ListGalleriesAsync).WithName("StudioListGalleries");
        galleries.MapGet("/{id:guid}", GetGalleryAsync).WithName("StudioGetGallery");

        return endpoints;
    }

    /// <summary>
    /// First step: the password. A remembered browser gets its session; any other gets a code in the email.
    /// </summary>
    /// <param name="request">Credentials.</param>
    /// <param name="context">The request being answered, to read and set cookies.</param>
    /// <param name="cookies">The cookies of the web area.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the user (session cookie set); 202 with where the code went (challenge cookie set); 401 for credentials that are not accepted; 403 for an account without a verified email; 429 for too many attempts or codes.</returns>
    private static async Task<Results<Ok<StudioSignedInResponse>, Accepted<StudioCodeRequiredResponse>>> LoginAsync(
        StudioLoginRequest request,
        HttpContext context,
        StudioCookies cookies,
        ICommandHandler<StudioLoginCommand, StudioLoginResult> handler,
        CancellationToken cancellationToken)
    {
        var deviceToken = cookies.Read(context.Request, StudioCookies.Device);
        var result = await handler.HandleAsync(new StudioLoginCommand(request.Username, request.Password, deviceToken), cancellationToken);

        switch (result)
        {
            case StudioSignedIn signedIn:
                cookies.Clear(context, StudioCookies.Challenge);
                cookies.Set(context, StudioCookies.Session, signedIn.Session.Token, signedIn.Session.ExpiresAt);
                cookies.Set(context, StudioCookies.Device, signedIn.Device.Token, signedIn.Device.ExpiresAt);
                return TypedResults.Ok(new StudioSignedInResponse("SignedIn", signedIn.Session.User));
            case StudioCodeRequired codeRequired:
                // A device cookie that did not get the browser in is dropped: it is not remembered any more, or it is another user's.
                if (deviceToken is not null)
                {
                    cookies.Clear(context, StudioCookies.Device);
                }

                cookies.Clear(context, StudioCookies.Session);
                cookies.Set(context, StudioCookies.Challenge, codeRequired.ChallengeToken, DateTimeOffset.UtcNow + StudioLoginChallenge.SendWindow);
                return TypedResults.Accepted((string?)null, new StudioCodeRequiredResponse("CodeRequired", codeRequired.MaskedEmail, codeRequired.ExpiresAt));
            default:
                throw new InvalidOperationException("Unknown result of the web sign-in.");
        }
    }

    /// <summary>
    /// Second step: the code from the email, which must come from the browser that gave the password.
    /// </summary>
    /// <param name="request">The code and whether to remember the browser.</param>
    /// <param name="context">The request being answered, to read and set cookies.</param>
    /// <param name="cookies">The cookies of the web area.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the user (session cookie set); 422 <c>auth.invalid_login_code</c> for a wrong, expired or spent code; 429 for too many attempts.</returns>
    private static async Task<Ok<StudioSignedInResponse>> VerifyAsync(
        StudioVerifyRequest request,
        HttpContext context,
        StudioCookies cookies,
        ICommandHandler<StudioVerifyCodeCommand, StudioVerifyResult> handler,
        CancellationToken cancellationToken)
    {
        var challengeToken = cookies.Read(context.Request, StudioCookies.Challenge);
        var result = await handler.HandleAsync(new StudioVerifyCodeCommand(challengeToken, request.Code, request.RememberDevice), cancellationToken);

        cookies.Clear(context, StudioCookies.Challenge);
        cookies.Set(context, StudioCookies.Session, result.Session.Token, result.Session.ExpiresAt);
        if (result.Device is not null)
        {
            cookies.Set(context, StudioCookies.Device, result.Device.Token, result.Device.ExpiresAt);
        }

        return TypedResults.Ok(new StudioSignedInResponse("SignedIn", result.Session.User));
    }

    /// <summary>
    /// Sends a new code to the sign-in that is waiting for it.
    /// </summary>
    /// <param name="context">The request being answered, to read cookies.</param>
    /// <param name="cookies">The cookies of the web area.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>202 with where the code went; 422 <c>auth.invalid_login_code</c> when the browser has no sign-in waiting; 429 <c>auth.login_code_rate_limited</c> with <c>Retry-After</c>.</returns>
    private static async Task<Accepted<StudioCodeSentResponse>> ResendAsync(
        HttpContext context,
        StudioCookies cookies,
        ICommandHandler<StudioResendCodeCommand, StudioCodeSentResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Accepted(
            (string?)null,
            await handler.HandleAsync(new StudioResendCodeCommand(cookies.Read(context.Request, StudioCookies.Challenge)), cancellationToken));

    /// <summary>
    /// Signs out: the session is closed on the server and its cookie dropped, whatever state the browser was in.
    /// </summary>
    /// <param name="request">Whether to also forget the browser; the body may be left out.</param>
    /// <param name="context">The request being answered, to read and clear cookies.</param>
    /// <param name="cookies">The cookies of the web area.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>204.</returns>
    private static async Task<NoContent> LogoutAsync(
        StudioLogoutRequest? request,
        HttpContext context,
        StudioCookies cookies,
        ICommandHandler<StudioLogoutCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        var forgetDevice = request?.ForgetDevice ?? false;
        await handler.HandleAsync(
            new StudioLogoutCommand(cookies.Read(context.Request, StudioCookies.Session), cookies.Read(context.Request, StudioCookies.Device), forgetDevice),
            cancellationToken);

        cookies.Clear(context, StudioCookies.Session);
        cookies.Clear(context, StudioCookies.Challenge);
        if (forgetDevice)
        {
            cookies.Clear(context, StudioCookies.Device);
        }

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Says who is signed in, so the page knows at load whether to show the sign-in or the galleries.
    /// </summary>
    /// <param name="user">The principal of the session.</param>
    /// <returns>200 with the user; 401 when there is no live session.</returns>
    private static Ok<StudioSessionResponse> GetSession(ClaimsPrincipal user) =>
        TypedResults.Ok(new StudioSessionResponse(new StudioUserSummary(
            user.GetPhotographerId(),
            user.FindFirstValue("preferred_username") ?? string.Empty,
            user.FindFirstValue("name") ?? string.Empty,
            string.Empty)));

    /// <summary>
    /// Lists one page of the galleries of the signed-in photographer.
    /// </summary>
    /// <param name="user">The principal of the session.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <param name="cursor">The <c>nextCursor</c> of the previous page; omitted for the first page.</param>
    /// <param name="limit">How many galleries the page has.</param>
    /// <returns>200 with the page.</returns>
    private static async Task<Ok<GalleryListResponse>> ListGalleriesAsync(
        ClaimsPrincipal user,
        IQueryHandler<ListGalleriesQuery, GalleryListResponse> handler,
        CancellationToken cancellationToken,
        string? cursor = null,
        int? limit = null) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ListGalleriesQuery(user.GetPhotographerId(), PageCursors.Read(cursor), PageSizes.Resolve(limit)),
            cancellationToken));

    /// <summary>
    /// Reads one gallery of the signed-in photographer.
    /// </summary>
    /// <param name="id">Gallery identifier.</param>
    /// <param name="user">The principal of the session.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the gallery; 404 when it does not exist or is another photographer's.</returns>
    private static async Task<Ok<GalleryResponse>> GetGalleryAsync(
        Guid id,
        ClaimsPrincipal user,
        IQueryHandler<GetGalleryQuery, GalleryResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetGalleryQuery(user.GetPhotographerId(), id), cancellationToken));
}
