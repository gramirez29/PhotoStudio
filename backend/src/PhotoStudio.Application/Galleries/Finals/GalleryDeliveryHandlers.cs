using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Portal;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Domain.Galleries;
using PhotoStudio.Domain.Galleries.Events;

namespace PhotoStudio.Application.Galleries.Finals;

/// <summary>
/// Delivers a gallery (G7) when its final photos are published and the balance of the booking is zero. It is the only place that
/// joins the two: the gallery does not know about money, and the booking does not know about photos. Delivering a gallery that is
/// already delivered, or whose balance is not zero, does nothing, so it is safe to call from every event that may change either.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="bookings">Booking repository, for the balance.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class GalleryDelivery(IGalleryRepository galleries, IBookingRepository bookings, TimeProvider timeProvider)
{
    private const int Attempts = 3;

    /// <summary>
    /// Delivers the gallery when it is ready and nothing is owed.
    /// </summary>
    /// <param name="gallery">The gallery, as loaded by the caller.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the gallery was delivered or there was nothing to do.</returns>
    public async Task TryDeliverAsync(Gallery? gallery, CancellationToken cancellationToken)
    {
        for (var attempt = 1; gallery is { Status: GalleryStatus.Ready }; attempt++)
        {
            var booking = await bookings.GetByIdAsync(gallery.BookingId, cancellationToken);
            if (booking is null || booking.PhotographerId != gallery.PhotographerId || booking.Balance.Amount > 0m)
            {
                return;
            }

            if (!gallery.Deliver(timeProvider.GetUtcNow()))
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
                gallery = await galleries.GetByIdAsync(gallery.Id, cancellationToken);
            }
        }
    }
}

/// <summary>
/// Delivers the gallery when the photographer publishes the final photos and nothing is owed.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="delivery">Delivers the gallery when the balance allows it.</param>
public sealed class FinalPhotosReadyDeliveryHandler(IGalleryRepository galleries, GalleryDelivery delivery) : IDomainEventHandler<FinalPhotosReady>
{
    /// <summary>
    /// Handles the event.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the delivery was tried.</returns>
    public async Task HandleAsync(FinalPhotosReady domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await delivery.TryDeliverAsync(await galleries.GetByIdAsync(domainEvent.GalleryId, cancellationToken), cancellationToken);
    }
}

/// <summary>
/// Delivers the gallery when the payment that clears the balance is verified, which may be weeks after the final photos were published.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="delivery">Delivers the gallery when the balance allows it.</param>
public sealed class PaymentVerifiedDeliveryHandler(IGalleryRepository galleries, GalleryDelivery delivery) : IDomainEventHandler<PaymentVerified>
{
    /// <summary>
    /// Handles the event.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the delivery was tried.</returns>
    public async Task HandleAsync(PaymentVerified domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await delivery.TryDeliverAsync(await galleries.GetByBookingIdAsync(domainEvent.BookingId, cancellationToken), cancellationToken);
    }
}

/// <summary>
/// Tells the client by email that the photos are ready to download, when the gallery is delivered. Like the email about the proofs it
/// carries no link, and it is sent once: the gallery records when the client was told. A booking without an email sends nothing.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="bookings">Booking repository, for the email of the client.</param>
/// <param name="email">Sends the email.</param>
/// <param name="portal">Public address of the site, where the logo is.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class GalleryDeliveredEmailHandler(
    IGalleryRepository galleries,
    IBookingRepository bookings,
    IEmailSender email,
    PortalSettings portal,
    TimeProvider timeProvider) : IDomainEventHandler<GalleryDelivered>
{
    private const int Attempts = 3;

    /// <summary>
    /// Sends the email, once.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the client was told, or there was nobody to tell.</returns>
    /// <exception cref="EmailDeliveryException">When the email provider did not accept the message; the outbox tries again later.</exception>
    public async Task HandleAsync(GalleryDelivered domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var gallery = await galleries.GetByIdAsync(domainEvent.GalleryId, cancellationToken);
        if (gallery is null || gallery.DeliveryNotifiedAt is not null)
        {
            return;
        }

        var address = (await bookings.GetByIdAsync(gallery.BookingId, cancellationToken))?.Client.Email;
        if (address is null)
        {
            return;
        }

        await email.SendAsync(GalleryEmails.Delivered(address, gallery, $"{portal.BaseUrl}/icon-192.png"), cancellationToken);

        // Recorded after sending: if this fails the event is delivered again and the client may get a second email, which is better
        // than none. The gallery is reloaded on a collision, because only this field changes.
        for (var attempt = 1; ; attempt++)
        {
            if (!gallery.MarkDeliveryNotified(timeProvider.GetUtcNow()))
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
