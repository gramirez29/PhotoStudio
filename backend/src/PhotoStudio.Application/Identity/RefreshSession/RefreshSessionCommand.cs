namespace PhotoStudio.Application.Identity.RefreshSession;

/// <summary>
/// Command to exchange a refresh token for a new session.
/// </summary>
/// <param name="RefreshToken">Refresh token secret held by the app.</param>
public sealed record RefreshSessionCommand(string RefreshToken);
