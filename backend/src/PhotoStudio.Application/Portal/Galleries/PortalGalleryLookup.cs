using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Galleries;
using PhotoStudio.Domain.Portal;

namespace PhotoStudio.Application.Portal.Galleries;

/// <summary>
/// Lookups and mappings shared by the portal use cases about the gallery.
/// </summary>
internal static class PortalGalleryLookup
{
    /// <summary>
    /// How long the address of a proof works for the client. It is the most the storage allows, and about the life of the session
    /// of the portal: a client who is choosing for a while does not see the photos disappear. The page asks again when it comes back.
    /// </summary>
    public static readonly TimeSpan UrlLifetime = TimeSpan.FromMinutes(20);

    /// <summary>How long the address to download a final photo works: short, because it cannot be revoked and the photo is the product.</summary>
    public static readonly TimeSpan FinalUrlLifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Loads the gallery the link opens, if the client can already see it. A booking without a gallery, a gallery that is still
    /// waiting for its proofs and one that belongs to another photographer are reported exactly the same way.
    /// </summary>
    /// <param name="galleries">Gallery repository.</param>
    /// <param name="link">Active link.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery.</returns>
    /// <exception cref="NotFoundException">When there is nothing for the client to see.</exception>
    public static async Task<Gallery> GetVisibleAsync(this IGalleryRepository galleries, BookingLink link, CancellationToken cancellationToken)
    {
        var gallery = await galleries.GetByBookingIdAsync(link.BookingId, cancellationToken);
        return gallery is null || gallery.PhotographerId != link.PhotographerId || gallery.Status == GalleryStatus.Pending
            ? throw new NotFoundException(nameof(Gallery), Guid.Empty)
            : gallery;
    }

    /// <summary>
    /// Maps a gallery to what its client sees, with a fresh signed address for each proof.
    /// </summary>
    /// <param name="gallery">The gallery.</param>
    /// <param name="storage">Object storage that signs the addresses.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The client's view.</returns>
    public static async Task<PortalGalleryResponse> ToPortalResponseAsync(this Gallery gallery, IObjectStorage storage, CancellationToken cancellationToken)
    {
        var photos = new List<PortalProofResponse>(gallery.Photos.Count);
        foreach (var photo in gallery.Photos)
        {
            var url = await storage.CreateDownloadUrlAsync(PhotoStudio.Application.Galleries.GalleryKeys.Proof(gallery.Id, photo.Id), UrlLifetime, null, cancellationToken);
            photos.Add(new PortalProofResponse(photo.Id, url.Url, url.ExpiresAt));
        }

        var selection = gallery.SelectionSubmittedAt is { } submittedAt
            ? new PortalSelectionResponse([.. gallery.SelectedPhotoIds], submittedAt, gallery.ExtraPhotoCount, gallery.ExtraPhotoTotal.ToResponse())
            : null;

        return new PortalGalleryResponse(
            gallery.Status,
            gallery.SelectablePhotos,
            gallery.ExtraPhotoPrice.ToResponse(),
            photos,
            selection,
            await DownloadableFinalsAsync(gallery, storage, cancellationToken),
            gallery.ExtrasReleasedAt is not null,
            gallery.Status == GalleryStatus.Delivered && gallery.ZipBytes is { } zipBytes ? new PortalZipResponse(zipBytes) : null);
    }

    /// <summary>
    /// Lists the final photos the client may take now: none until the gallery is delivered (the balance is zero), then those of the
    /// package, and those of the extras only once the photographer released them. It carries no address: the client asks for them
    /// when they download, so every download is counted and no address is waiting somewhere for someone to find.
    /// </summary>
    /// <param name="gallery">The gallery.</param>
    /// <param name="storage">Object storage that signs the addresses of the small images.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The downloadable finals, in the order the client chose them.</returns>
    public static async Task<IReadOnlyList<PortalFinalResponse>> DownloadableFinalsAsync(Gallery gallery, IObjectStorage storage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gallery);

        var finals = new List<PortalFinalResponse>();
        foreach (var photoId in gallery.SelectedPhotoIds.Where(gallery.IsDownloadable))
        {
            var photo = gallery.Photos.First(candidate => candidate.Id == photoId);
            Uri? thumb = null;
            if (photo.FinalThumbBytes is not null)
            {
                // A small image to look at: not a download, so asking for it is not counted.
                thumb = (await storage.CreateDownloadUrlAsync(PhotoStudio.Application.Galleries.GalleryKeys.Thumb(gallery.Id, photoId), UrlLifetime, null, cancellationToken)).Url;
            }

            finals.Add(new PortalFinalResponse(photoId, FinalFileName(photo), photo.FinalBytes ?? 0, gallery.IsExtra(photoId), thumb));
        }

        return finals;
    }

    /// <summary>
    /// Builds the name the client saves a final photo with: the name of the original file, as a JPEG.
    /// </summary>
    /// <param name="photo">The photo.</param>
    /// <returns>The file name.</returns>
    public static string FinalFileName(GalleryPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        return Path.GetFileNameWithoutExtension(photo.OriginalName) + ".jpg";
    }
}
