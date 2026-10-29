using Microsoft.Extensions.Logging;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Bookings.Events;

namespace PhotoStudio.Application.Contracts;

/// <summary>
/// Emails the client a copy of the contract they signed through the portal, the same address that receives their verification
/// codes. The outbox delivers an event at least once, so the handler remembers in the signature when the copy went out and does
/// nothing the second time. A contract the photographer registered in person or on paper has no text to send, and a booking
/// without a client email has nowhere to send it, so both are skipped.
/// </summary>
/// <param name="bookings">Booking repository.</param>
/// <param name="email">Sends the email.</param>
/// <param name="timeProvider">Clock abstraction.</param>
/// <param name="logger">Logger.</param>
public sealed partial class ContractCopyHandler(
    IBookingRepository bookings,
    IEmailSender email,
    TimeProvider timeProvider,
    ILogger<ContractCopyHandler> logger) : IDomainEventHandler<ContractSigned>
{
    /// <summary>How many times the handler tries to record that the copy was sent when another write keeps changing the booking.</summary>
    private const int MarkAttempts = 3;

    /// <summary>
    /// Sends the copy, unless it is not needed or was already sent. A failure to send makes the outbox retry later; a failure only
    /// to record the shipment is logged and not retried, because retrying would send the copy again.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the copy was sent or skipped.</returns>
    /// <exception cref="EmailDeliveryException">When the email provider did not accept the copy.</exception>
    public async Task HandleAsync(ContractSigned domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.Channel != Channel.Portal)
        {
            return;
        }

        var booking = await bookings.GetByIdAsync(domainEvent.BookingId, cancellationToken);
        if (booking?.Contract is not { SignedText: { } signedText, CopySentAt: null } || booking.Client.Email is not { } address)
        {
            return;
        }

        await email.SendAsync(ContractEmails.SignedCopy(address, booking.Client.Name, signedText), cancellationToken);
        await MarkSentAsync(booking.Id, cancellationToken);
    }

    /// <summary>
    /// Records that the copy was sent. When another write changed the booking first, it loads the booking again and retries; a
    /// booking that already says so is left alone.
    /// </summary>
    /// <param name="bookingId">Booking whose copy was sent.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the shipment is recorded or the attempts ran out.</returns>
    private async Task MarkSentAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MarkAttempts; attempt++)
        {
            var booking = await bookings.GetByIdAsync(bookingId, cancellationToken);
            if (booking?.Contract is null || booking.Contract.CopySentAt is not null)
            {
                return;
            }

            booking.MarkContractCopySent(timeProvider.GetUtcNow());
            try
            {
                await bookings.UpdateAsync(booking, cancellationToken);
                return;
            }
            catch (ConflictException)
            {
                // Another write won; the next attempt reads the booking as it is now.
            }
        }

        LogShipmentNotRecorded(logger, bookingId);
    }

    /// <summary>
    /// Logs that the copy went out but could not be recorded, so a later delivery of the same event may send it once more.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="bookingId">Booking whose copy was sent.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The copy of the contract of booking {BookingId} was sent but could not be recorded after several attempts.")]
    private static partial void LogShipmentNotRecorded(ILogger logger, Guid bookingId);
}
