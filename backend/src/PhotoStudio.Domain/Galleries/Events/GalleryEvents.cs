using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Galleries.Events;

/// <summary>Raised when the photographer opens the selection (G2): the proofs are published and the client can choose.</summary>
/// <param name="GalleryId">Gallery identifier.</param>
/// <param name="BookingId">Booking the gallery comes from.</param>
/// <param name="PhotographerId">Photographer identifier.</param>
/// <param name="OccurredAt">Instant of the event (UTC).</param>
public sealed record SelectionOpened(Guid GalleryId, Guid BookingId, Guid PhotographerId, DateTimeOffset OccurredAt) : IDomainEvent;
