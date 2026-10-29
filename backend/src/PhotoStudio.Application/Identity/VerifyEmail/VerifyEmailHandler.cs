using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.VerifyEmail;

/// <summary>
/// Checks the code the user typed and, when it is right, records that they own their email. The check is the one of the client
/// portal: each guess is counted atomically before it is compared, five wrong guesses spend the code, a code works once, and the
/// answer to a wrong, expired or spent code is always the same. Verifying changes what the token can do, so it answers with a new
/// session, and the other sessions of the account are closed, like after a change of password.
/// </summary>
/// <param name="users">User repository.</param>
/// <param name="codes">Stores the code.</param>
/// <param name="codeService">Hashes and compares codes.</param>
/// <param name="refreshTokens">Refresh token repository.</param>
/// <param name="sessions">Issues the new session.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class VerifyEmailHandler(
    IUserRepository users,
    IEmailVerificationRepository codes,
    IOtpCodeService codeService,
    IRefreshTokenRepository refreshTokens,
    SessionIssuer sessions,
    TimeProvider timeProvider) : ICommandHandler<VerifyEmailCommand, AuthSessionResponse>
{
    /// <summary>
    /// Verifies the email.
    /// </summary>
    /// <param name="command">User and code.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new session whose token lets the user into the app.</returns>
    /// <exception cref="AuthenticationFailedException">When the user of the token no longer exists.</exception>
    /// <exception cref="DomainException">When the code is wrong, expired or spent.</exception>
    public async Task<AuthSessionResponse> HandleAsync(VerifyEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var user = await users.GetByIdAsync(command.PhotographerId, cancellationToken)
            ?? throw new AuthenticationFailedException(ApplicationErrorCodes.InvalidCredentials, "The session is no longer valid. Sign in again.");

        if (!user.IsEmailVerified)
        {
            await CheckCodeAsync(user, command.Code, now, cancellationToken);
            await users.MarkEmailVerifiedAsync(user.Id, now, cancellationToken);
            user = await users.GetByIdAsync(user.Id, cancellationToken)
                ?? throw new AuthenticationFailedException(ApplicationErrorCodes.InvalidCredentials, "The session is no longer valid. Sign in again.");
        }

        // The token of the session that verified still says "not verified", and so does any other the account has: they are all
        // closed and this device gets a new one. Doing it again for an account that was already verified is harmless.
        await refreshTokens.RevokeAllForUserAsync(user.Id, now, cancellationToken);
        var session = sessions.Issue(user, Guid.CreateVersion7(), now);
        await refreshTokens.AddAsync(session.RefreshToken, cancellationToken);
        return session.Response;
    }

    /// <summary>
    /// Compares the code the user typed with the stored one, counting the guess first.
    /// </summary>
    /// <param name="user">User verifying.</param>
    /// <param name="code">What the user typed.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the code is accepted and spent.</returns>
    /// <exception cref="DomainException">When the code is wrong, expired or spent.</exception>
    private async Task CheckCodeAsync(User user, string? code, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var typed = (code ?? string.Empty).Trim();
        if (typed.Length != EmailVerification.CodeLength || !typed.All(char.IsAsciiDigit))
        {
            throw InvalidCode();
        }

        var stored = await codes.RegisterAttemptAsync(user.Id, cancellationToken);
        if (stored is null)
        {
            throw InvalidCode();
        }

        if (stored.IsExpiredAt(now) || stored.FailedAttempts > EmailVerification.MaxFailedAttempts)
        {
            await codes.DeleteAsync(user.Id, cancellationToken);
            throw InvalidCode();
        }

        if (!codeService.Matches(stored.CodeHash, codeService.Hash(user.Id, typed)))
        {
            if (stored.FailedAttempts >= EmailVerification.MaxFailedAttempts)
            {
                await codes.DeleteAsync(user.Id, cancellationToken);
            }

            throw InvalidCode();
        }

        // A code is single-use: it is gone as soon as it verifies the email.
        await codes.DeleteAsync(user.Id, cancellationToken);
    }

    /// <summary>
    /// Builds the one error the user gets for any code that is not accepted, so the answer says nothing about why.
    /// </summary>
    /// <returns>The error.</returns>
    private static DomainException InvalidCode() =>
        new(ApplicationErrorCodes.InvalidEmailCode, "The code is not correct or has expired. Ask for a new one.");
}
