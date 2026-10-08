using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.Login;

/// <summary>
/// Signs a photographer in. Wrong credentials, an unknown email and an invalid email all produce the same
/// <see cref="AuthenticationFailedException"/>, after the same amount of hashing work, so the response does not reveal
/// which emails have an account. Consecutive failures lock the account for <see cref="PhotographerAccount.LockoutDuration"/>.
/// </summary>
/// <param name="accounts">Account repository.</param>
/// <param name="refreshTokens">Refresh token repository.</param>
/// <param name="passwordHasher">Password hashing.</param>
/// <param name="sessions">Builds the session tokens.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class LoginHandler(
    IPhotographerAccountRepository accounts,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    SessionIssuer sessions,
    TimeProvider timeProvider) : ICommandHandler<LoginCommand, AuthSessionResponse>
{
    /// <summary>
    /// Verifies the credentials and starts a session.
    /// </summary>
    /// <param name="command">Email and password.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The new session.</returns>
    /// <exception cref="AuthenticationFailedException">When the credentials are not accepted.</exception>
    /// <exception cref="AccountLockedException">When the account is locked by too many failed attempts.</exception>
    public async Task<AuthSessionResponse> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var password = command.Password ?? string.Empty;
        var email = PhotographerAccount.TryNormalizeEmail(command.Email);
        var account = email is null ? null : await accounts.GetByEmailAsync(email, cancellationToken);

        if (account is null)
        {
            passwordHasher.SpendVerificationTime(password);
            throw InvalidCredentials();
        }

        if (account.IsLockedAt(now))
        {
            throw new AccountLockedException(account.LockedUntil!.Value - now);
        }

        if (!passwordHasher.Verify(account.PasswordHash, password))
        {
            var failedAttempts = await accounts.RegisterFailedLoginAsync(account.Id, cancellationToken);
            if (PhotographerAccount.ShouldLock(failedAttempts))
            {
                await accounts.LockAsync(account.Id, now + PhotographerAccount.LockoutDuration, cancellationToken);
            }

            throw InvalidCredentials();
        }

        if (account.FailedLoginAttempts > 0 || account.LockedUntil is not null)
        {
            await accounts.ResetFailedLoginsAsync(account.Id, cancellationToken);
        }

        // Every login starts a new family, so signing out of one device never signs out the others.
        var session = sessions.Issue(account.Id, account.Email, Guid.CreateVersion7(), now);
        await refreshTokens.AddAsync(session.RefreshToken, cancellationToken);
        return session.Response;
    }

    /// <summary>
    /// Builds the generic failure used for every rejected login.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    private static AuthenticationFailedException InvalidCredentials() =>
        new(ApplicationErrorCodes.InvalidCredentials, "The email or the password is not correct.");
}
