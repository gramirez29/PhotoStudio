using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity;

/// <summary>
/// A session that was just issued: the refresh token to store and the response to send to the app.
/// </summary>
/// <param name="RefreshToken">Refresh token entity (holds only the hash); not stored yet.</param>
/// <param name="Response">Response for the app, which carries the secret of the refresh token.</param>
public sealed record IssuedSession(RefreshToken RefreshToken, AuthSessionResponse Response);

/// <summary>
/// Builds the tokens of a session. Shared by register, login and refresh so all produce sessions the same way. It does not
/// store anything: each use case decides how the refresh token is persisted (inserted on register and login, swapped
/// atomically on refresh).
/// </summary>
/// <param name="accessTokens">Issues the access token.</param>
/// <param name="refreshTokens">Generates the refresh token secret and hash.</param>
/// <param name="settings">Session lifetimes.</param>
public sealed class SessionIssuer(
    IAccessTokenIssuer accessTokens,
    IRefreshTokenGenerator refreshTokens,
    AuthSessionSettings settings)
{
    /// <summary>
    /// Issues the tokens of a session.
    /// </summary>
    /// <param name="user">User the session belongs to.</param>
    /// <param name="familyId">Family of the login the refresh token belongs to.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The session, with its refresh token not yet stored.</returns>
    public IssuedSession Issue(User user, Guid familyId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(user);

        var access = accessTokens.Issue(user.Id, user.Username, now);
        var generated = refreshTokens.Generate();
        var refreshToken = RefreshToken.Issue(user.Id, familyId, generated.Hash, now, settings.RefreshTokenLifetime);

        return new IssuedSession(
            refreshToken,
            new AuthSessionResponse(access.Value, access.ExpiresAt, generated.Secret, refreshToken.ExpiresAt, user.Id, user.Username, user.Email, user.Name));
    }
}
