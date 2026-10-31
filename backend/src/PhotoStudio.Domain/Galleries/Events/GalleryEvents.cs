using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Galleries.Events;

/// <summary>Raised when the photographer opens the selection (G2): the proofs are published and the client can choose.</summary>
/// <param name="GalleryId">Gallery identifier.</param>
/// <param name="BookingId">Booking the gallery comes from.</param>
/// <param name="PhotographerId">Photographer identifier.</param>
/// <param name="OccurredAt">Instant of the event (UTC).</param>
public sealed record SelectionOpened(Guid GalleryId, Guid BookingId, Guid PhotographerId, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Raised when the client sends the selection (G3): the photos to edit are chosen and the extra ones can be charged.</summary>
/// <param name="GalleryId">Gallery identifier.</param>
/// <param name="BookingId">Booking the gallery comes from.</param>
/// <param name="PhotographerId">Photographer identifier.</param>
/// <param name="SelectedPhotos">How many photos the client chose.</param>
/// <param name="ExtraPhotos">How many of them go beyond what the gallery includes.</param>
/// <param name="ExtraTotal">What the extra photos cost: the price frozen in the gallery times <paramref name="ExtraPhotos"/>.</param>
/// <param name="Currency">Currency of <paramref name="ExtraTotal"/>.</param>
/// <param name="OccurredAt">Instant of the event (UTC).</param>
public sealed record SelectionSubmitted(Guid GalleryId, Guid BookingId, Guid PhotographerId, int SelectedPhotos, int ExtraPhotos, decimal ExtraTotal, string Currency, DateTimeOffset OccurredAt) : IDomainEvent;
