using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Portal;
using PhotoStudio.Domain.Galleries.Events;

namespace PhotoStudio.Application.Galleries;

/// <summary>
/// Tells the client by email that the proofs are ready, when the selection is opened. The outbox delivers an event at least once, so
/// the gallery records when the client was told and a second delivery sends nothing. A booking without an email sends nothing (the
/// photographer tells the client by WhatsApp). The email carries no link: see <see cref="GalleryEmails.SelectionOpened"/>.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="bookings">Booking repository, for the email of the client.</param>
/// <param name="email">Sends the email.</param>
/// <param name="portal">Public address of the site, where the logo is.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class SelectionOpenedEmailHandler(
    IGalleryRepository galleries,
    IBookingRepository bookings,
    IEmailSender email,
    PortalSettings portal,
    TimeProvider timeProvider) : IDomainEventHandler<SelectionOpened>
{
    private const int Attempts = 3;

    /// <summary>
    /// Sends the email, once.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the client was told, or there was nobody to tell.</returns>
    /// <exception cref="EmailDeliveryException">When the email provider did not accept the message; the outbox tries again later.</exception>
    public async Task HandleAsync(SelectionOpened domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var gallery = await galleries.GetByIdAsync(domainEvent.GalleryId, cancellationToken);
        if (gallery is null || gallery.SelectionNotifiedAt is not null)
        {
            return;
        }

        var address = (await bookings.GetByIdAsync(gallery.BookingId, cancellationToken))?.Client.Email;
        if (address is null)
        {
            return;
        }

        await email.SendAsync(GalleryEmails.SelectionOpened(address, gallery, $"{portal.BaseUrl}/icon-192.png"), cancellationToken);

        // Recorded after sending: if this fails the event is delivered again and the client may get a second email, which is better
        // than none. The gallery is reloaded on a collision, because only this field changes.
        for (var attempt = 1; ; attempt++)
        {
            if (!gallery.MarkClientNotified(timeProvider.GetUtcNow()))
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
