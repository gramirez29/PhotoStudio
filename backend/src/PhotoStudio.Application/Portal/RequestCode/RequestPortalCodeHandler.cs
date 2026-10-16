using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Portal;

namespace PhotoStudio.Application.Portal.RequestCode;

/// <summary>
/// Sends a verification code to the email the photographer registered for the client. The client never types an address:
/// the code can only go where the photographer said, so holding the link is not enough to receive it somewhere else.
/// Requests are limited per booking (a cooldown between codes and a cap per hour), on top of the per-address limit of the API.
/// </summary>
/// <param name="bookings">Booking repository.</param>
/// <param name="links">Link repository.</param>
/// <param name="codes">Verification code repository.</param>
/// <param name="codeService">Generates and hashes the codes.</param>
/// <param name="tokens">Hashes the token taken from the URL.</param>
/// <param name="email">Email sender.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class RequestPortalCodeHandler(
    IBookingRepository bookings,
    IBookingLinkRepository links,
    IPortalOtpRepository codes,
    IOtpCodeService codeService,
    IRefreshTokenGenerator tokens,
    IEmailSender email,
    TimeProvider timeProvider) : ICommandHandler<RequestPortalCodeCommand, PortalCodeSentResponse>
{
    /// <summary>
    /// Creates a code and emails it.
    /// </summary>
    /// <param name="command">Token of the link.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Where the code went (masked) and when it expires.</returns>
    /// <exception cref="NotFoundException">When the token does not open a booking.</exception>
    /// <exception cref="ConflictException">When the booking has no client email.</exception>
    /// <exception cref="OtpRateLimitedException">When a code was requested too soon or too many times.</exception>
    /// <exception cref="EmailDeliveryException">When the email could not be sent.</exception>
    public async Task<PortalCodeSentResponse> HandleAsync(RequestPortalCodeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var link = await links.ResolveAsync(tokens, command.Token, now, cancellationToken);
        var booking = await bookings.GetBookingAsync(link, cancellationToken);

        var address = booking.Client.Email
            ?? throw new ConflictException(ApplicationErrorCodes.EmailMissing, "This booking has no email to send the code to. Ask the photographer to add it.");

        var previous = await codes.GetAsync(booking.Id, cancellationToken);
        if (previous?.RetryAfterAt(now) is { } retryAfter)
        {
            throw new OtpRateLimitedException(retryAfter);
        }

        var code = codeService.Generate();
        var record = PortalOtp.Issue(booking.Id, codeService.Hash(booking.Id, code), previous, now);

        // The record is stored first and the email sent after: if the email fails the client sees the error and can ask again
        // once the cooldown passes, and a code that was stored but never delivered is harmless (nobody knows it).
        await codes.SaveAsync(record, cancellationToken);
        await email.SendAsync(PortalEmails.VerificationCode(address, booking.Client.Name, code), cancellationToken);

        return new PortalCodeSentResponse(PortalLinkExtensions.MaskEmail(address), record.ExpiresAt);
    }
}
