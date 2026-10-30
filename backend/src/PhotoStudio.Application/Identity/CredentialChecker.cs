using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity;

/// <summary>
/// Checks a username and a password, for every way of signing in. A wrong password, an unknown username and an invalid username
/// all produce the same <see cref="AuthenticationFailedException"/>, after the same amount of hashing work, so the answer does not
/// reveal which usernames exist. Consecutive failures lock the user for <see cref="User.LockoutDuration"/>, and the count is the
/// same whichever way the user tries to sign in.
/// </summary>
/// <param name="users">User repository.</param>
/// <param name="passwordHasher">Password hashing.</param>
public sealed class CredentialChecker(IUserRepository users, IPasswordHasher passwordHasher)
{
    /// <summary>
    /// Verifies the credentials.
    /// </summary>
    /// <param name="username">Username as typed.</param>
    /// <param name="password">Password as typed.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The user, with their failed attempts cleared.</returns>
    /// <exception cref="AuthenticationFailedException">When the credentials are not accepted.</exception>
    /// <exception cref="AccountLockedException">When the user is locked by too many failed attempts.</exception>
    public async Task<User> VerifyAsync(string? username, string? password, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var typedPassword = password ?? string.Empty;
        var normalized = User.TryNormalizeUsername(username);
        var user = normalized is null ? null : await users.GetByUsernameAsync(normalized, cancellationToken);

        if (user is null)
        {
            passwordHasher.SpendVerificationTime(typedPassword);
            throw InvalidCredentials();
        }

        if (user.IsLockedAt(now))
        {
            throw new AccountLockedException(user.LockedUntil!.Value - now);
        }

        if (!passwordHasher.Verify(user.PasswordHash, typedPassword))
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

        return user;
    }

    /// <summary>
    /// Builds the generic failure used for every rejected sign-in.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    private static AuthenticationFailedException InvalidCredentials() =>
        new(ApplicationErrorCodes.InvalidCredentials, "The username or the password is not correct.");
}
