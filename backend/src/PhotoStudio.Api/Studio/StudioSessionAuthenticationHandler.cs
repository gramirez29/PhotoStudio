using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PhotoStudio.Application.Identity.Studio;

namespace PhotoStudio.Api.Studio;

/// <summary>
/// Authenticates a request of the web area from the session cookie. The cookie is not a token with data inside: it is a secret
/// that is looked up on the server on every request, so closing a session, changing the password or locking the account takes effect
/// at once. The principal it builds carries the scope <c>studio</c>, which is what the policy of the web area asks for and what no
/// token of the app has.
/// </summary>
public sealed class StudioSessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// Name of the authentication scheme.
    /// </summary>
    public const string SchemeName = "StudioSession";

    /// <summary>
    /// Name of the claim that says what the session may do.
    /// </summary>
    public const string ScopeClaimType = "scope";

    /// <summary>
    /// Value of <see cref="ScopeClaimType"/> for the web area.
    /// </summary>
    public const string StudioScope = "studio";

    private readonly StudioCookies _cookies;

    /// <summary>
    /// Initializes a new instance of the <see cref="StudioSessionAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">Options of the scheme.</param>
    /// <param name="logger">Logger factory.</param>
    /// <param name="encoder">URL encoder.</param>
    /// <param name="cookies">The cookies of the web area.</param>
    public StudioSessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        StudioCookies cookies)
        : base(options, logger, encoder)
    {
        _cookies = cookies;
    }

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = _cookies.Read(Request, StudioCookies.Session);
        if (token is null)
        {
            return AuthenticateResult.NoResult();
        }

        var validator = Context.RequestServices.GetRequiredService<StudioSessionValidator>();
        var user = await validator.ValidateAsync(token, Context.RequestAborted);
        if (user is null)
        {
            return AuthenticateResult.NoResult();
        }

        var identity = new ClaimsIdentity(
            [
                new Claim("sub", user.PhotographerId.ToString()),
                new Claim("preferred_username", user.Username),
                new Claim("name", user.Name),
                new Claim(ScopeClaimType, StudioScope),
            ],
            SchemeName,
            nameType: "sub",
            roleType: "role");
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
