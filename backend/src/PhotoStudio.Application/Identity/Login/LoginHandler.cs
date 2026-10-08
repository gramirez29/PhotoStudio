using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.Login;

/// <summary>
/// Signs a user in. Wrong credentials, an unknown username and an invalid username all produce the same
/// <see cref="AuthenticationFailedException"/>, after the same amount of hashing work, so the response does not reveal
/// which usernames exist. Consecutive failures lock the user for <see cref="User.LockoutDuration"/>.
/// </summary>
/// <param name="users">User repository.</param>
/// <param name="refreshTokens">Refresh token repository.</param>
/// <param name="passwordHasher">Password hashing.</param>
/// <param name="sessions">Builds the session tokens.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class LoginHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    SessionIssuer sessions,
    TimeProvider timeProvider) : ICommandHandler<LoginCommand, AuthSessionResponse>
{
    /// <summary>
    /// Verifies the credentials and starts a session.
    /// </summary>
    /// <param name="command">Username and password.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The new session.</returns>
    /// <exception cref="AuthenticationFailedException">When the credentials are not accepted.</exception>
    /// <exception cref="AccountLockedException">When the user is locked by too many failed attempts.</exception>
    public async Task<AuthSessionResponse> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var password = command.Password ?? string.Empty;
        var username = User.TryNormalizeUsername(command.Username);
        var user = username is null ? null : await users.GetByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            passwordHasher.SpendVerificationTime(password);
            throw InvalidCredentials();
        }

        if (user.IsLockedAt(now))
        {
            throw new AccountLockedException(user.LockedUntil!.Value - now);
        }

        if (!passwordHasher.Verify(user.PasswordHash, password))
        {
            var failedAttempts = await users.RegisterFailedLoginAsync(user.Id, cancellationToken);
            if (User.ShouldLock(failedAttempts))
            {
                await users.LockAsync(user.Id, now + User.LockoutDuration, cancellationToken);
            }

            throw InvalidCredentials();
        }

        if (user.FailedLoginAttempts > 0 || user.LockedUntil is not null)
        {
            await users.ResetFailedLoginsAsync(user.Id, cancellationToken);
        }

        // Every login starts a new family, so signing out of one device never signs out the others.
        var session = sessions.Issue(user, Guid.CreateVersion7(), now);
        await refreshTokens.AddAsync(session.RefreshToken, cancellationToken);
        return session.Response;
    }

    /// <summary>
    /// Builds the generic failure used for every rejected login.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    private static AuthenticationFailedException InvalidCredentials() =>
        new(ApplicationErrorCodes.InvalidCredentials, "The username or the password is not correct.");
}
