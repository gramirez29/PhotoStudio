namespace PhotoStudio.Application.Identity;

/// <summary>
/// A signed-in session: the tokens the app needs and who it belongs to.
/// </summary>
/// <param name="AccessToken">Short-lived JWT sent as <c>Authorization: Bearer</c>.</param>
/// <param name="AccessTokenExpiresAt">Instant the access token stops being accepted (UTC).</param>
/// <param name="RefreshToken">Secret that obtains a new session; the app keeps it in secure storage.</param>
/// <param name="RefreshTokenExpiresAt">Instant the refresh token stops being valid (UTC).</param>
/// <param name="PhotographerId">Photographer (tenant) identifier.</param>
/// <param name="Email">Photographer email.</param>
public sealed record AuthSessionResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid PhotographerId,
    string Email);
