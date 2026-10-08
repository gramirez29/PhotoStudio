using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace PhotoStudio.Api.Auth;

/// <summary>
/// Reads the identity of the authenticated photographer from the validated access token.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Gets the photographer (tenant) identifier: the <c>sub</c> claim of the access token. It is the only source of the
    /// tenant for the API; nothing the client sends in a body or a query string can override it.
    /// </summary>
    /// <param name="user">The authenticated user of the request.</param>
    /// <returns>The photographer identifier.</returns>
    /// <exception cref="InvalidOperationException">When the token carries no valid subject. A token this API issued always does, so this signals a configuration problem, not a client error.</exception>
    public static Guid GetPhotographerId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(subject, out var photographerId) || photographerId == Guid.Empty)
        {
            throw new InvalidOperationException("The access token has no valid photographer identifier.");
        }

        return photographerId;
    }
}
