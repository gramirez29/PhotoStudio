using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Identity.Studio;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.ChangePassword;

/// <summary>
/// Changes the password of the signed-in user. It asks for the current password even though the user has a valid token,
/// because a stolen phone or access token must not be enough to take over the account. A wrong current password counts
/// as a failed login (same counter and lockout as the login), so this endpoint cannot be used to guess the password.
/// On success every session of the user is closed and a new one is issued to the device that made the change.
/// </summary>
/// <param name="users">User repository.</param>
/// <param name="refreshTokens">Refresh token repository.</param>
/// <param name="passwordHasher">Password hashing.</param>
/// <param name="sessions">Builds the session tokens.</param>
/// <param name="studioAccess">Revokes what the web area trusts about the user: sessions, remembered browsers and pending code.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class ChangePasswordHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    SessionIssuer sessions,
    StudioAccessRevoker studioAccess,
    TimeProvider timeProvider) : ICommandHandler<ChangePasswordCommand, AuthSessionResponse>
{
    /// <summary>
    /// Verifies the current password, stores the new one and replaces every session of the user.
    /// </summary>
    /// <param name="command">User, current password and new password.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The new session of the device that changed the password.</returns>
    /// <exception cref="AuthenticationFailedException">When the user no longer exists.</exception>
    /// <exception cref="AccountLockedException">When the user is locked by too many failed attempts.</exception>
    /// <exception cref="DomainException">When the current password is wrong, or the new one is weak or equal to the current one.</exception>
    /// <exception cref="ConflictException">When the password was changed by another request meanwhile.</exception>
    public async Task<AuthSessionResponse> HandleAsync(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var user = await users.GetByIdAsync(command.PhotographerId, cancellationToken)
            ?? throw new AuthenticationFailedException(ApplicationErrorCodes.InvalidCredentials, "The session is no longer valid. Sign in again.");

        if (user.IsLockedAt(now))
        {
            throw new AccountLockedException(user.LockedUntil!.Value - now);
        }

        var currentPassword = command.CurrentPassword ?? string.Empty;
        if (!passwordHasher.Verify(user.PasswordHash, currentPassword))
        {
            var failedAttempts = await users.RegisterFailedLoginAsync(user.Id, cancellationToken);
            if (User.ShouldLock(failedAttempts))
            {
                await users.LockAsync(user.Id, now + User.LockoutDuration, cancellationToken);
            }

            throw new DomainException(ApplicationErrorCodes.InvalidCurrentPassword, "The current password is not correct.");
        }

        User.ValidatePassword(command.NewPassword);
        if (string.Equals(currentPassword, command.NewPassword, StringComparison.Ordinal))
        {
            throw new DomainException(DomainErrorCodes.WeakPassword, "The new password must be different from the current one.");
        }

        var changed = await users.ChangePasswordAsync(user.Id, user.PasswordHash, passwordHasher.Hash(command.NewPassword), cancellationToken);
        if (!changed)
        {
            throw new ConflictException(ApplicationErrorCodes.ConcurrencyConflict, "The password was changed by another request. Try again.");
        }

        // Anyone who had a session with the old password (a stolen refresh token included) is signed out; this device gets a new one.
        await refreshTokens.RevokeAllForUserAsync(user.Id, now, cancellationToken);

        // The same for the web area: its sessions, the browsers it remembered and any code it was waiting for.
        await studioAccess.RevokeAllAsync(user.Id, cancellationToken);
        var session = sessions.Issue(user, Guid.CreateVersion7(), now);
        await refreshTokens.AddAsync(session.RefreshToken, cancellationToken);
        return session.Response;
    }
}
