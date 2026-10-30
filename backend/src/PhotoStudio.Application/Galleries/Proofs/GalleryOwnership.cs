using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.Galleries.Proofs;

/// <summary>
/// Tenant-aware lookup of a gallery: every use case that receives a gallery identifier from a client loads it through here, so a
/// photographer can never read or change the gallery of another one.
/// </summary>
internal static class GalleryOwnership
{
    /// <summary>
    /// Loads a gallery that belongs to the photographer. A gallery of another photographer is reported exactly like a missing one,
    /// so the answer never reveals that the identifier exists.
    /// </summary>
    /// <param name="galleries">Gallery repository.</param>
    /// <param name="galleryId">Gallery identifier.</param>
    /// <param name="photographerId">Authenticated photographer (tenant).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery.</returns>
    /// <exception cref="NotFoundException">When the gallery does not exist or belongs to another photographer.</exception>
    public static async Task<Gallery> GetOwnedAsync(this IGalleryRepository galleries, Guid galleryId, Guid photographerId, CancellationToken cancellationToken)
    {
        var gallery = await galleries.GetByIdAsync(galleryId, cancellationToken);
        return gallery is null || gallery.PhotographerId != photographerId ? throw new NotFoundException(nameof(Gallery), galleryId) : gallery;
    }
}
