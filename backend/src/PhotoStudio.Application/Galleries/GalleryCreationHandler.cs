using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.Galleries;

/// <summary>
/// Creates the gallery of a booking when its session is completed (G1). Nobody asks for it: the photographer only completes
/// the session. The outbox delivers events at least once, so a second delivery finds the gallery already there and does
/// nothing; the unique index on the booking decides, not a "look first, then insert".
/// </summary>
/// <param name="bookings">Booking repository.</param>
/// <param name="galleries">Gallery repository.</param>
/// <param name="policies">Supplies how many photos each package includes and the price of an extra one.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class GalleryCreationHandler(
    IBookingRepository bookings,
    IGalleryRepository galleries,
    IGalleryPolicyProvider policies,
    TimeProvider timeProvider) : IDomainEventHandler<SessionCompleted>
{
    /// <summary>
    /// Creates the gallery of the completed session, copying the policy that applies today.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the gallery exists.</returns>
    public async Task HandleAsync(SessionCompleted domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var booking = await bookings.GetByIdAsync(domainEvent.BookingId, cancellationToken);
        if (booking is null)
        {
            return;
        }

        var policy = await policies.GetPolicyAsync(booking.PhotographerId, cancellationToken);
        var gallery = Gallery.Create(
            booking.PhotographerId,
            booking.Id,
            new GallerySnapshot(booking.Client.Name, booking.PackageName, booking.Slot.Start),
            policy.IncludedFor(booking.PackageTier),
            Money.Create(policy.ExtraPhotoPrice, booking.PackagePrice.Currency),
            timeProvider.GetUtcNow());

        await galleries.TryAddAsync(gallery, cancellationToken);
    }
}
