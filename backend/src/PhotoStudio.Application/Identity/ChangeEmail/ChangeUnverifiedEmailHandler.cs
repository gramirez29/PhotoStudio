using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Identity.ResendEmailVerification;
using PhotoStudio.Application.Portal;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.ChangeEmail;

/// <summary>
/// Replaces the email of an account that has not proved it yet and sends the code to the new address. It exists for the person who
/// misspelled their email when registering and can never receive the code. It is refused once the email is verified, because from
/// then on the address is a proof of ownership and changing it needs more than a session. The limits of the code (a minute between
/// codes, five an hour) are checked <b>before</b> anything changes and are not reset by the change, so this cannot be used to send
/// mail to many addresses through the service.
/// </summary>
/// <param name="users">User repository.</param>
/// <param name="codes">Stores the record of the code, whose counters carry the limits.</param>
/// <param name="sender">Creates the code and sends it.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class ChangeUnverifiedEmailHandler(
    IUserRepository users,
    IEmailVerificationRepository codes,
    EmailVerificationSender sender,
    TimeProvider timeProvider) : ICommandHandler<ChangeUnverifiedEmailCommand, EmailVerificationSentResponse>
{
    /// <summary>
    /// Changes the email and sends the code.
    /// </summary>
    /// <param name="command">User and the address they want.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Where the code went (masked) and when it expires.</returns>
    /// <exception cref="AuthenticationFailedException">When the user of the token no longer exists.</exception>
    /// <exception cref="DomainException">When the address has no valid shape.</exception>
    /// <exception cref="ConflictException">When the email is already verified, or another account uses the address.</exception>
    /// <exception cref="OtpRateLimitedException">When a code was sent less than a minute ago or too many times in the last hour.</exception>
    /// <exception cref="EmailDeliveryException">When the email provider did not accept the message.</exception>
    public async Task<EmailVerificationSentResponse> HandleAsync(ChangeUnverifiedEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var email = User.NormalizeEmail(command.Email);
        var user = await users.GetByIdAsync(command.PhotographerId, cancellationToken)
            ?? throw new AuthenticationFailedException(ApplicationErrorCodes.InvalidCredentials, "The session is no longer valid. Sign in again.");

        if (user.IsEmailVerified)
        {
            throw new ConflictException(ApplicationErrorCodes.EmailAlreadyVerified, "Your email is already verified.");
        }

        // The limits come first: a refusal must leave the account as it was.
        var previous = await codes.GetAsync(user.Id, cancellationToken);
        if (previous?.RetryAfterAt(now) is { } retryAfter)
        {
            throw new OtpRateLimitedException(retryAfter, ApplicationErrorCodes.EmailCodeRateLimited);
        }

        if (!string.Equals(user.Email, email, StringComparison.Ordinal))
        {
            // False means the email got verified between the read and the write; nothing was changed.
            if (!await users.ChangeUnverifiedEmailAsync(user.Id, email, cancellationToken))
            {
                throw new ConflictException(ApplicationErrorCodes.EmailAlreadyVerified, "Your email is already verified.");
            }

            user = await users.GetByIdAsync(user.Id, cancellationToken)
                ?? throw new AuthenticationFailedException(ApplicationErrorCodes.InvalidCredentials, "The session is no longer valid. Sign in again.");
        }

        var record = await sender.SendAsync(user, now, cancellationToken);
        return new EmailVerificationSentResponse(PortalLinkExtensions.MaskEmail(user.Email), record.ExpiresAt);
    }
}
