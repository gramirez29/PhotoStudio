namespace PhotoStudio.Application.Galleries.GetGallery;

/// <summary>
/// Query for one gallery of a photographer, by its identifier.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery identifier.</param>
public sealed record GetGalleryQuery(Guid PhotographerId, Guid GalleryId);

/// <summary>
/// Query for the gallery of a booking of a photographer.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="BookingId">Booking identifier.</param>
public sealed record GetGalleryByBookingQuery(Guid PhotographerId, Guid BookingId);
