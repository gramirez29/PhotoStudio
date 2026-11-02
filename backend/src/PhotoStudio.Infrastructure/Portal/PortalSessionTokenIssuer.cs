using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Infrastructure.Identity;

namespace PhotoStudio.Infrastructure.Portal;

/// <summary>
/// Issues the portal session tokens as signed JWTs. They are signed with the same key as the photographer's tokens but carry
/// a different audience and a <c>bkg</c> claim with the booking they open, so the validation of each kind rejects the other:
/// a client token never reaches a photographer endpoint and a photographer token never opens the portal.
/// </summary>
public sealed class PortalSessionTokenIssuer : IPortalSessionIssuer
{
    /// <summary>
    /// Name of the claim that carries the booking the token opens.
    /// </summary>
    public const string BookingClaimType = "bkg";

    /// <summary>
    /// Audience of the portal tokens.
    /// </summary>
    public const string Audience = "photosense-portal";

    /// <summary>
    /// How long a portal session lasts when nothing is configured (see <see cref="PortalSessionLifetime"/>). It is not renewable: the
    /// client confirms a new code afterwards.
    /// </summary>
    public static readonly TimeSpan DefaultLifetime = PortalSessionLifetime.Default;

    private readonly JwtOptions _options;
    private readonly TimeSpan _lifetime;
    private readonly SigningCredentials _credentials;
    private readonly JsonWebTokenHandler _handler = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalSessionTokenIssuer"/> class.
    /// </summary>
    /// <param name="options">Token settings.</param>
    /// <param name="lifetime">How long a session lasts after the client confirms the code.</param>
    public PortalSessionTokenIssuer(JwtOptions options, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "A session must last for some time.");
        }

        _options = options;
        _lifetime = lifetime;
        _credentials = new SigningCredentials(JwtAccessTokenIssuer.CreateKey(options), SecurityAlgorithms.HmacSha256);
    }

    /// <inheritdoc />
    public AccessToken Issue(Guid bookingId, DateTimeOffset now)
    {
        var expiresAt = now + _lifetime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
            Subject = new ClaimsIdentity(
            [
                new Claim(BookingClaimType, bookingId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
