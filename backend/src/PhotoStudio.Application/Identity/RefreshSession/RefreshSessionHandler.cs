using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;

namespace PhotoStudio.Application.Identity.RefreshSession;

/// <summary>
/// Exchanges a refresh token for a new session and rotates it: the presented token is revoked and replaced by a new one of
/// the same family. A token that was already revoked means it was used twice, which only happens when it was copied, so the
/// whole family is revoked and the owner has to sign in again.
/// </summary>
/// <param name="users">User repository.</param>
/// <param name="refreshTokens">Refresh token repository.</param>
/// <param name="tokenGenerator">Hashes the presented secret.</param>
/// <param name="sessions">Builds the session tokens.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class RefreshSessionHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IRefreshTokenGenerator tokenGenerator,
    SessionIssuer sessions,
    TimeProvider timeProvider) : ICommandHandler<RefreshSessionCommand, AuthSessionResponse>
{
    /// <summary>
    /// Rotates the refresh token and issues a new session.
    /// </summary>
    /// <param name="command">Refresh token secret.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The new session.</returns>
    /// <exception cref="AuthenticationFailedException">When the token is unknown, expired, already used or its user no longer exists.</exception>
    public async Task<AuthSessionResponse> HandleAsync(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            throw Invalid();
        }

        var now = timeProvider.GetUtcNow();
        var current = await refreshTokens.GetByHashAsync(tokenGenerator.Hash(command.RefreshToken), cancellationToken)
            ?? throw Invalid();

        if (current.IsRevoked)
        {
            // A token is revoked only after it was rotated or its session was closed, so presenting it again is a replay.
            await refreshTokens.RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            throw Invalid();
        }

        if (current.IsExpiredAt(now))
        {
            throw Invalid();
        }

        var user = await users.GetByIdAsync(current.PhotographerId, cancellationToken);
        if (user is null)
        {
            await refreshTokens.RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            throw Invalid();
        }

        var session = sessions.Issue(user, current.FamilyId, now);
        if (!await refreshTokens.RotateAsync(current, session.RefreshToken, now, cancellationToken))
        {
            // Another request used the same token between our read and our write.
            await refreshTokens.RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            throw Invalid();
        }

        return session.Response;
    }

    /// <summary>
    /// Builds the generic failure used for every rejected refresh token.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    private static AuthenticationFailedException Invalid() =>
        new(ApplicationErrorCodes.InvalidRefreshToken, "The session is no longer valid. Sign in again.");
}
