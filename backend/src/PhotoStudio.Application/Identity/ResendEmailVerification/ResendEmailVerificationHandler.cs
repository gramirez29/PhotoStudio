using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Portal;

namespace PhotoStudio.Application.Identity.ResendEmailVerification;

/// <summary>
/// Sends a new code to verify the email of the signed-in user. The code only goes to the address of the account, which the user
/// never types here, so asking for it cannot be used to send mail to somebody else.
/// </summary>
/// <param name="users">User repository.</param>
/// <param name="sender">Creates the code and sends it.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class ResendEmailVerificationHandler(
    IUserRepository users,
    EmailVerificationSender sender,
    TimeProvider timeProvider) : ICommandHandler<ResendEmailVerificationCommand, EmailVerificationSentResponse>
{
    /// <summary>
    /// Sends the code.
    /// </summary>
    /// <param name="command">User asking for the code.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Where the code went (masked) and when it expires.</returns>
    /// <exception cref="AuthenticationFailedException">When the user of the token no longer exists.</exception>
    /// <exception cref="ConflictException">When the account has no email or its email is already verified.</exception>
    /// <exception cref="OtpRateLimitedException">When a code was sent less than a minute ago or too many times in the last hour.</exception>
    /// <exception cref="EmailDeliveryException">When the email provider did not accept the message.</exception>
    public async Task<EmailVerificationSentResponse> HandleAsync(ResendEmailVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.GetByIdAsync(command.PhotographerId, cancellationToken)
            ?? throw new AuthenticationFailedException(ApplicationErrorCodes.InvalidCredentials, "The session is no longer valid. Sign in again.");

        if (user.Email.Length == 0)
        {
            throw new ConflictException(ApplicationErrorCodes.AccountEmailMissing, "This account has no email to verify.");
        }

        if (user.IsEmailVerified)
        {
            throw new ConflictException(ApplicationErrorCodes.EmailAlreadyVerified, "Your email is already verified.");
        }

        var record = await sender.SendAsync(user, timeProvider.GetUtcNow(), cancellationToken);
        return new EmailVerificationSentResponse(PortalLinkExtensions.MaskEmail(user.Email), record.ExpiresAt);
    }
}
