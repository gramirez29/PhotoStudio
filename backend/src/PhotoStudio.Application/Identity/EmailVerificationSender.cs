using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity;

/// <summary>
/// Creates a code to verify the email of an account and sends it to that address. Registering and asking again share it, so the
/// limits of the code (a minute between codes, five an hour) are the same in both.
/// </summary>
/// <param name="codes">Stores the record of the code.</param>
/// <param name="codeService">Generates and hashes codes.</param>
/// <param name="email">Sends the email.</param>
public sealed class EmailVerificationSender(
    IEmailVerificationRepository codes,
    IOtpCodeService codeService,
    IEmailSender email)
{
    /// <summary>
    /// Sends a new code to the address of the user.
    /// </summary>
    /// <param name="user">User whose email is verified; it must have one.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The record of the code that was sent, with its expiry.</returns>
    /// <exception cref="OtpRateLimitedException">When a code was sent less than a minute ago or too many times in the last hour.</exception>
    /// <exception cref="EmailDeliveryException">When the email provider did not accept the message.</exception>
    public async Task<EmailVerification> SendAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var previous = await codes.GetAsync(user.Id, cancellationToken);
        if (previous?.RetryAfterAt(now) is { } retryAfter)
        {
            throw new OtpRateLimitedException(retryAfter, ApplicationErrorCodes.EmailCodeRateLimited);
        }

        var code = codeService.Generate();
        var record = EmailVerification.Issue(user.Id, codeService.Hash(user.Id, code), previous, now);

        // The record is stored first and the email sent after: if the email fails the user can ask again once the cooldown passes,
        // and a code that was stored but never delivered is harmless (nobody knows it).
        await codes.SaveAsync(record, cancellationToken);
        await email.SendAsync(IdentityEmails.VerificationCode(user.Email, user.Name, code), cancellationToken);
        return record;
    }
}
