using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Infrastructure.Identity;

/// <summary>
/// Issues the access tokens as signed JWTs (HMAC-SHA256). The token carries only what the API needs to authorize a request:
/// the photographer identifier as <c>sub</c> (the tenant of every query), the username, a unique <c>jti</c> and, only for
/// system operators, the <c>role</c> claim that opens the system-wide endpoints.
/// </summary>
public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    /// <summary>
    /// Name of the claim that carries the role of the user.
    /// </summary>
    public const string SystemRoleClaimType = "role";

    /// <summary>
    /// Value of the role claim of a user who may run system-wide work.
    /// </summary>
    public const string SystemRoleValue = "system";

    /// <summary>
    /// Name of the claim that says whether the user may use the app: <c>true</c>, or <c>false</c> while they have not verified their email.
    /// </summary>
    public const string EmailVerifiedClaimType = "email_verified";

    private readonly JwtOptions _options;
    private readonly SigningCredentials _credentials;
    private readonly JsonWebTokenHandler _handler = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtAccessTokenIssuer"/> class.
    /// </summary>
    /// <param name="options">Token settings.</param>
    public JwtAccessTokenIssuer(JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _credentials = new SigningCredentials(CreateKey(options), SecurityAlgorithms.HmacSha256);
    }

    /// <summary>
    /// Builds the symmetric key used to sign and validate tokens, shared with the validation setup.
    /// </summary>
    /// <param name="options">Token settings.</param>
    /// <returns>The key.</returns>
    public static SymmetricSecurityKey CreateKey(JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
    }

    /// <inheritdoc />
    public AccessToken Issue(Guid photographerId, string username, bool isSystemOperator, bool emailVerified, DateTimeOffset now)
    {
        var expiresAt = now + _options.AccessTokenLifetime;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, photographerId.ToString()),
            new(JwtRegisteredClaimNames.PreferredUsername, username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(EmailVerifiedClaimType, emailVerified ? "true" : "false"),
        };
        if (isSystemOperator)
        {
            claims.Add(new Claim(SystemRoleClaimType, SystemRoleValue));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
            Subject = new ClaimsIdentity(claims),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
