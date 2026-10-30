using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.Studio;

/// <summary>
/// Decides, on every request, whether the cookie of a browser stands for a live session. The session must exist, must not be idle
/// or past its fixed end, and its user must still exist with a verified email and not be locked: a session of an account that was
/// locked or lost its email is closed on the spot. Using it pushes the "last seen" instant forward, at most once a minute.
/// </summary>
/// <param name="sessions">Session repository.</param>
/// <param name="users">User repository.</param>
/// <param name="tokens">Hashes the secret of the cookie.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class StudioSessionValidator(
    IStudioSessionRepository sessions,
    IUserRepository users,
    IRefreshTokenGenerator tokens,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Validates the secret of a session cookie.
    /// </summary>
    /// <param name="token">Secret of the cookie, if the browser sent one.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Who the session belongs to, or <see langword="null"/> when it is not a live session.</returns>
    public async Task<StudioUserSummary?> ValidateAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var session = await sessions.GetByTokenHashAsync(tokens.Hash(token), cancellationToken);
        if (session is null)
        {
            return null;
        }

        if (!session.IsValidAt(now))
        {
            await sessions.DeleteAsync(session.Id, cancellationToken);
            return null;
        }

        var user = await users.GetByIdAsync(session.UserId, cancellationToken);
        if (user is null || !user.IsEmailVerified || user.IsLockedAt(now))
        {
            await sessions.DeleteAsync(session.Id, cancellationToken);
            return null;
        }

        if (session.ShouldTouchAt(now))
        {
            await sessions.TouchAsync(session.Id, now, cancellationToken);
        }

        return StudioSessionIssuer.Summarize(user);
    }
}
