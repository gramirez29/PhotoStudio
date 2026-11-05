using Microsoft.Extensions.Logging;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Identity.Studio;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.RecoverPassword;

/// <summary>
/// Chooses a new password for a user who forgot the old one, with the code that was sent to their verified email. The check is the one
/// of the other codes: each guess is counted atomically before it is compared, five wrong guesses spend the code, a code works once,
/// and the answer to a wrong, expired or spent code, to an unknown account and to an account that cannot recover its password is
/// always the same. The new password is validated first, so a weak one does not spend the code. On success every session of the
/// user is closed (the person who forgot the password may have lost the phone), the web area is cut off too, a lockout from failed
/// logins is lifted, and the owner is told by email that the password changed. Nobody is signed in afterwards: the user signs in with
/// the new password.
/// </summary>
/// <param name="users">User repository.</param>
/// <param name="codes">Stores the code.</param>
/// <param name="codeService">Hashes and compares codes.</param>
/// <param name="passwordHasher">Password hashing.</param>
/// <param name="refreshTokens">Refresh token repository.</param>
/// <param name="studioAccess">Revokes what the web area trusts about the user.</param>
/// <param name="email">Sends the notice of the change.</param>
/// <param name="timeProvider">Clock abstraction.</param>
/// <param name="logger">Logger.</param>
public sealed partial class ConfirmPasswordResetHandler(
    IUserRepository users,
    IPasswordResetRepository codes,
    IOtpCodeService codeService,
    IPasswordHasher passwordHasher,
    IRefreshTokenRepository refreshTokens,
    StudioAccessRevoker studioAccess,
    IEmailSender email,
    TimeProvider timeProvider,
    ILogger<ConfirmPasswordResetHandler> logger) : ICommandHandler<ConfirmPasswordResetCommand, PasswordResetResponse>
{
    /// <summary>
    /// Checks the code and stores the new password.
    /// </summary>
    /// <param name="command">Account, code and new password.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The confirmation.</returns>
    /// <exception cref="DomainException">When the new password is not valid, or the code is wrong, expired or spent.</exception>
    /// <exception cref="ConflictException">When the password was changed by another request meanwhile.</exception>
    public async Task<PasswordResetResponse> HandleAsync(ConfirmPasswordResetCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Cheap checks first, and before the code is counted: a password that is too short must not spend a guess.
        User.ValidatePassword(command.NewPassword);
        var typed = (command.Code ?? string.Empty).Trim();
        if (typed.Length != PasswordResetCode.CodeLength || !typed.All(char.IsAsciiDigit))
        {
            throw InvalidCode();
        }

        var now = timeProvider.GetUtcNow();
        var user = await new AccountFinder(users).FindRecoverableAsync(command.Identifier, cancellationToken) ?? throw InvalidCode();
        await CheckCodeAsync(user, typed, now, cancellationToken);

        var changed = await users.ChangePasswordAsync(user.Id, user.PasswordHash, passwordHasher.Hash(command.NewPassword), cancellationToken);
        if (!changed)
        {
            throw new ConflictException(ApplicationErrorCodes.ConcurrencyConflict, "The password was changed by another request. Try again.");
        }

        await codes.DeleteAsync(user.Id, cancellationToken);
        await refreshTokens.RevokeAllForUserAsync(user.Id, now, cancellationToken);
        await studioAccess.RevokeAllAsync(user.Id, cancellationToken);
        await NotifyAsync(user, cancellationToken);
        return new PasswordResetResponse();
    }

    /// <summary>
    /// Compares the code the user typed with the stored one, counting the guess first.
    /// </summary>
    /// <param name="user">User recovering the password.</param>
    /// <param name="typed">Six digits typed.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the code is accepted.</returns>
    /// <exception cref="DomainException">When the code is wrong, expired or spent.</exception>
    private async Task CheckCodeAsync(User user, string typed, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var stored = await codes.RegisterAttemptAsync(user.Id, cancellationToken);
        if (stored is null)
        {
            throw InvalidCode();
        }

        if (stored.IsExpiredAt(now) || stored.FailedAttempts > PasswordResetCode.MaxFailedAttempts)
        {
            await codes.DeleteAsync(user.Id, cancellationToken);
            throw InvalidCode();
        }

        if (!codeService.Matches(stored.CodeHash, codeService.Hash(user.Id, PasswordResetCode.HashPurpose + typed)))
        {
            if (stored.FailedAttempts >= PasswordResetCode.MaxFailedAttempts)
            {
                await codes.DeleteAsync(user.Id, cancellationToken);
            }

            throw InvalidCode();
        }
    }

    /// <summary>
    /// Tells the owner of the account that the password changed, so a recovery they did not ask for does not go unnoticed. A failure
    /// is logged and swallowed: the password is already changed.
    /// </summary>
    /// <param name="user">The user whose password changed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the notice was sent or the failure was logged.</returns>
    private async Task NotifyAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            await email.SendAsync(IdentityEmails.PasswordChanged(user.Email, user.Name), cancellationToken);
        }
        catch (EmailDeliveryException exception)
        {
            LogNoticeFailed(logger, exception, user.Id);
        }
    }

    /// <summary>
    /// Builds the one error the user gets for any code that is not accepted, so the answer says nothing about why.
    /// </summary>
    /// <returns>The error.</returns>
    private static DomainException InvalidCode() =>
        new(ApplicationErrorCodes.InvalidPasswordResetCode, "The code is not correct or has expired. Ask for a new one.");

    /// <summary>
    /// Logs that the notice of the password change could not be sent.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="exception">Why the email failed.</param>
    /// <param name="userId">The user whose password changed.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The notice that the password of user {UserId} changed could not be sent.")]
    private static partial void LogNoticeFailed(ILogger logger, Exception exception, Guid userId);
}
