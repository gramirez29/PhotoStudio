using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.Galleries;

/// <summary>
/// A gallery as exposed to the photographer.
/// </summary>
/// <param name="Id">Gallery identifier.</param>
/// <param name="BookingId">Booking it comes from.</param>
/// <param name="Status">Where it is in its life.</param>
/// <param name="ClientName">Name of the client.</param>
/// <param name="PackageName">Name of the booked package.</param>
/// <param name="SessionStart">Start of the session (UTC).</param>
/// <param name="IncludedPhotos">Photos the package includes.</param>
/// <param name="BonusPhotos">Photos given on top.</param>
/// <param name="SelectablePhotos">Photos the client can pick without paying more (included plus gift).</param>
/// <param name="ExtraPhotoPrice">Price of each photo beyond that.</param>
/// <param name="MaxPhotos">Most photos a gallery holds.</param>
/// <param name="PhotoCount">Proofs registered so far.</param>
/// <param name="CreatedAt">Instant it was created (UTC).</param>
/// <param name="AllowedActions">What the photographer can do with it now.</param>
/// <param name="SelectedPhotoCount">How many photos the client chose; 0 until they send the selection.</param>
/// <param name="ExtraPhotoCount">How many of those go beyond what the package includes.</param>
/// <param name="SelectionSubmittedAt">Instant the client sent the selection (UTC), or <see langword="null"/> while they did not.</param>
public sealed record GalleryResponse(
    Guid Id,
    Guid BookingId,
    GalleryStatus Status,
    string ClientName,
    string PackageName,
    DateTimeOffset SessionStart,
    int IncludedPhotos,
    int BonusPhotos,
    int SelectablePhotos,
    MoneyResponse ExtraPhotoPrice,
    int MaxPhotos,
    int PhotoCount,
    DateTimeOffset CreatedAt,
    IReadOnlyList<GalleryAction> AllowedActions,
    int SelectedPhotoCount = 0,
    int ExtraPhotoCount = 0,
    DateTimeOffset? SelectionSubmittedAt = null);

/// <summary>
/// One page of the galleries of a photographer, the most recently created first.
/// </summary>
/// <param name="Items">The galleries of this page.</param>
/// <param name="NextCursor">What to send back to get the next page, or <see langword="null"/> when this is the last one.</param>
public sealed record GalleryListResponse(IReadOnlyList<GalleryResponse> Items, string? NextCursor);

/// <summary>
/// Mapping from galleries to their API responses.
/// </summary>
public static class GalleryResponseMappings
{
    /// <summary>
    /// Maps a gallery to its response.
    /// </summary>
    /// <param name="gallery">The gallery.</param>
    /// <returns>The response.</returns>
    public static GalleryResponse ToResponse(this Gallery gallery)
    {
        ArgumentNullException.ThrowIfNull(gallery);

        return new GalleryResponse(
            gallery.Id,
            gallery.BookingId,
            gallery.Status,
            gallery.Snapshot.ClientName,
            gallery.Snapshot.PackageName,
            gallery.Snapshot.SessionStart,
            gallery.IncludedPhotos,
            gallery.BonusPhotos,
            gallery.SelectablePhotos,
            gallery.ExtraPhotoPrice.ToResponse(),
            Gallery.MaxPhotos,
            gallery.Photos.Count,
            gallery.CreatedAt,
            gallery.GetAllowedActions(),
            gallery.SelectedPhotoIds.Count,
            gallery.ExtraPhotoCount,
            gallery.SelectionSubmittedAt);
    }
}
