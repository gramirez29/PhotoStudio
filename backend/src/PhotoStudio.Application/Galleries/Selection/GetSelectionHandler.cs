using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Galleries.Proofs;

namespace PhotoStudio.Application.Galleries.Selection;

/// <summary>
/// Query for the photos a client chose in a gallery of the photographer.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery whose selection is read.</param>
public sealed record GetSelectionQuery(Guid PhotographerId, Guid GalleryId);

/// <summary>
/// A photo the client chose.
/// </summary>
/// <param name="Id">Photo identifier.</param>
/// <param name="Name">Name of the file the photographer uploaded it from: what to look for in Lightroom.</param>
/// <param name="IsExtra">Whether it is beyond what the package covers (the first photos chosen are the package, the rest are extras).</param>
/// <param name="FinalBytes">Size of its final photo, or <see langword="null"/> while the photographer has not uploaded it.</param>
/// <param name="FinalWidth">Width of the final in pixels, or <see langword="null"/> when unknown.</param>
/// <param name="FinalHeight">Height of the final in pixels, or <see langword="null"/> when unknown.</param>
/// <param name="FinalCapturedAt">Instant the final was taken (UTC), or <see langword="null"/> when its metadata did not say.</param>
/// <param name="FinalDownloads">How many times the client asked to download the final.</param>
/// <param name="ThumbUrl">Signed address of a small image to look at (valid for ten minutes): the thumbnail of the final when it has one, otherwise the proof, which has the watermark.</param>
/// <param name="ThumbIsFinal">Whether <paramref name="ThumbUrl"/> is the thumbnail of the final and not the proof.</param>
public sealed record SelectedPhotoResponse(
    Guid Id,
    string Name,
    bool IsExtra = false,
    long? FinalBytes = null,
    int? FinalWidth = null,
    int? FinalHeight = null,
    DateTimeOffset? FinalCapturedAt = null,
    int FinalDownloads = 0,
    Uri? ThumbUrl = null,
    bool ThumbIsFinal = false);

/// <summary>
/// What the client chose, for the photographer to edit.
/// </summary>
/// <param name="SubmittedAt">Instant the client sent it (UTC), or <see langword="null"/> while they did not.</param>
/// <param name="ExtraPhotos">How many of the photos go beyond what the package includes.</param>
/// <param name="ExtraTotal">What those extra photos cost.</param>
/// <param name="Photos">The photos chosen, in the order they were chosen, with the name of their original file.</param>
public sealed record SelectionResponse(DateTimeOffset? SubmittedAt, int ExtraPhotos, MoneyResponse ExtraTotal, IReadOnlyList<SelectedPhotoResponse> Photos);

/// <summary>
/// Gives the photographer the photos their client chose, with the names of the original files so they can be found in the
/// editing software.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="storage">Object storage that signs the addresses of the small images.</param>
public sealed class GetSelectionHandler(IGalleryRepository galleries, IObjectStorage storage) : IQueryHandler<GetSelectionQuery, SelectionResponse>
{
    /// <summary>
    /// How long the addresses of the small images work: long enough to look at the gallery, short because they cannot be revoked.
    /// </summary>
    private static readonly TimeSpan ThumbLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Reads the selection.
    /// </summary>
    /// <param name="query">Photographer and gallery.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The selection; empty while the client has not sent one.</returns>
    /// <exception cref="Exceptions.NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    public async Task<SelectionResponse> HandleAsync(GetSelectionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gallery = await galleries.GetOwnedAsync(query.GalleryId, query.PhotographerId, cancellationToken);
        var names = gallery.Photos.ToDictionary(photo => photo.Id, photo => photo.OriginalName);
        var byId = gallery.Photos.ToDictionary(photo => photo.Id);
        var photos = new List<SelectedPhotoResponse>(gallery.SelectedPhotoIds.Count);
        foreach (var id in gallery.SelectedPhotoIds.Where(names.ContainsKey))
        {
            var photo = byId[id];
            var hasThumb = photo.FinalThumbBytes is not null;
            var key = hasThumb ? GalleryKeys.Thumb(gallery.Id, id) : GalleryKeys.Proof(gallery.Id, id);
            var url = await storage.CreateDownloadUrlAsync(key, ThumbLifetime, null, cancellationToken);
            photos.Add(new SelectedPhotoResponse(
                id, names[id], gallery.IsExtra(id), photo.FinalBytes, photo.FinalWidth, photo.FinalHeight, photo.FinalCapturedAt, photo.FinalDownloads, url.Url, hasThumb));
        }

        return new SelectionResponse(gallery.SelectionSubmittedAt, gallery.ExtraPhotoCount, gallery.ExtraPhotoTotal.ToResponse(), photos);
    }
}
