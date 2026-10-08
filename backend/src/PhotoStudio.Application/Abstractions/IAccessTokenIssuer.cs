namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// An access token ready to send to the app.
/// </summary>
/// <param name="Value">The signed token (a JWT).</param>
/// <param name="ExpiresAt">Instant the token stops being accepted (UTC).</param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// Port that issues the short-lived access tokens the API accepts. Implemented in the Infrastructure layer.
/// </summary>
public interface IAccessTokenIssuer
{
    /// <summary>
    /// Issues an access token for a photographer.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier; becomes the subject of the token.</param>
    /// <param name="username">Username of the photographer.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The token and its expiry.</returns>
    AccessToken Issue(Guid photographerId, string username, DateTimeOffset now);
}
