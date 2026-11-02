using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Galleries.Events;

namespace PhotoStudio.Application.Galleries;

/// <summary>
/// Tells the client by email that the photographer reopened their selection. The outbox delivers an event at least once, so the
/// gallery records the last reopening the client was told about and a second delivery of the same event sends nothing; a later
/// reopening is told again. A booking without an email sends nothing. The email carries no link.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="bookings">Booking repository, for the email of the client.</param>
/// <param name="email">Sends the email.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class SelectionReopenedEmailHandler(
    IGalleryRepository galleries,
    IBookingRepository bookings,
    IEmailSender email,
    TimeProvider timeProvider) : IDomainEventHandler<SelectionReopened>
{
    private const int Attempts = 3;

    /// <summary>
    /// Sends the email, once per reopening.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the client was told, or there was nobody to tell.</returns>
    /// <exception cref="EmailDeliveryException">When the email provider did not accept the message; the outbox tries again later.</exception>
    public async Task HandleAsync(SelectionReopened domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var gallery = await galleries.GetByIdAsync(domainEvent.GalleryId, cancellationToken);
        if (gallery is null || gallery.ReopenNotifiedAt is { } told && told >= domainEvent.OccurredAt)
        {
            return;
        }

        var address = (await bookings.GetByIdAsync(gallery.BookingId, cancellationToken))?.Client.Email;
        if (address is null)
        {
            return;
        }

        await email.SendAsync(GalleryEmails.SelectionReopened(address, gallery), cancellationToken);

        // Recorded after sending: if this fails the event is delivered again and the client may get a second email, which is better
        // than none. The gallery is reloaded on a collision, because only this field changes.
        for (var attempt = 1; ; attempt++)
        {
            if (!gallery.MarkReopenNotified(domainEvent.OccurredAt, timeProvider.GetUtcNow()))
            {
                return;
            }

            try
            {
                await galleries.UpdateAsync(gallery, cancellationToken);
                return;
            }
            catch (ConflictException) when (attempt < Attempts)
            {
                gallery = await galleries.GetByIdAsync(domainEvent.GalleryId, cancellationToken);
                if (gallery is null)
                {
                    return;
                }
            }
        }
    }
}
