using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity;

/// <summary>
/// Checks a username and a password, for every way of signing in. A wrong password, an unknown username and an invalid username
/// all produce the same <see cref="AuthenticationFailedException"/>, after the same amount of hashing work, so the answer does not
/// reveal which usernames exist. The guesses are limited by <see cref="LoginAttemptGuard"/> (per account and network address, and
/// for the account as a whole), and the count is the same whichever way the user tries to sign in.
/// </summary>
/// <param name="users">User repository.</param>
/// <param name="passwordHasher">Password hashing.</param>
/// <param name="guard">Limits the guesses.</param>
public sealed class CredentialChecker(IUserRepository users, IPasswordHasher passwordHasher, LoginAttemptGuard guard)
{
    /// <summary>
    /// Verifies the credentials.
    /// </summary>
    /// <param name="username">Username as typed.</param>
    /// <param name="password">Password as typed.</param>
    /// <param name="address">Network address of the caller, if known.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The user, with their failed attempts cleared.</returns>
    /// <exception cref="AuthenticationFailedException">When the credentials are not accepted.</exception>
    /// <exception cref="AccountLockedException">When the user is locked, or this address is blocked for it, by too many failed attempts.</exception>
    public async Task<User> VerifyAsync(string? username, string? password, string? address, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var typedPassword = password ?? string.Empty;
        var normalized = User.TryNormalizeUsername(username);
        var user = normalized is null ? null : await users.GetByUsernameAsync(normalized, cancellationToken);

        if (user is null)
        {
            passwordHasher.SpendVerificationTime(typedPassword);
            throw InvalidCredentials();
        }

        await guard.EnsureAllowedAsync(user, address, now, cancellationToken);

        if (!passwordHasher.Verify(user.PasswordHash, typedPassword))
        {
            await guard.RegisterFailureAsync(user, address, now, cancellationToken);
            throw InvalidCredentials();
        }

        await guard.RegisterSuccessAsync(user, address, cancellationToken);
        return user;
    }

    /// <summary>
    /// Builds the generic failure used for every rejected sign-in.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    private static AuthenticationFailedException InvalidCredentials() =>
        new(ApplicationErrorCodes.InvalidCredentials, "The username or the password is not correct.");
}
