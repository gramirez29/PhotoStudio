namespace PhotoStudio.Application.Identity;

/// <summary>
/// Lifetimes of an authenticated session, configured from the environment.
/// </summary>
/// <param name="RefreshTokenLifetime">How long a refresh token stays valid. Using it issues a new one, so an active app stays signed in.</param>
public sealed record AuthSessionSettings(TimeSpan RefreshTokenLifetime);
