namespace PhotoStudio.Application.Identity.Logout;

/// <summary>
/// Command to close a session.
/// </summary>
/// <param name="RefreshToken">Refresh token secret of the session to close.</param>
public sealed record LogoutCommand(string RefreshToken);

/// <summary>
/// Outcome of closing a session.
/// </summary>
/// <param name="SessionClosed"><see langword="true"/> when a session was found and revoked; <see langword="false"/> when the token was unknown.</param>
public sealed record LogoutResponse(bool SessionClosed);
