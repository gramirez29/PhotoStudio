using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.Galleries.GetGallery;

/// <summary>
/// Reads one gallery. One that belongs to another photographer answers not found, exactly like one that does not exist, so the
/// identifier of someone else's gallery is not confirmed to exist.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
public sealed class GetGalleryHandler(IGalleryRepository galleries) : IQueryHandler<GetGalleryQuery, GalleryResponse>
{
    /// <summary>
    /// Reads the gallery.
    /// </summary>
    /// <param name="query">Photographer and gallery.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery.</returns>
    /// <exception cref="NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    public async Task<GalleryResponse> HandleAsync(GetGalleryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gallery = await galleries.GetByIdAsync(query.GalleryId, cancellationToken);
        if (gallery is null || gallery.PhotographerId != query.PhotographerId)
        {
            throw new NotFoundException(nameof(Gallery), query.GalleryId);
        }

        return gallery.ToResponse();
    }
}

/// <summary>
/// Reads the gallery of a booking. A booking without a gallery (its session was not completed) and a booking of another
/// photographer answer the same: not found.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
public sealed class GetGalleryByBookingHandler(IGalleryRepository galleries) : IQueryHandler<GetGalleryByBookingQuery, GalleryResponse>
{
    /// <summary>
    /// Reads the gallery.
    /// </summary>
    /// <param name="query">Photographer and booking.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery.</returns>
    /// <exception cref="NotFoundException">When the booking has no gallery or it is not the photographer's.</exception>
    public async Task<GalleryResponse> HandleAsync(GetGalleryByBookingQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gallery = await galleries.GetByBookingIdAsync(query.BookingId, cancellationToken);
        if (gallery is null || gallery.PhotographerId != query.PhotographerId)
        {
            throw new NotFoundException(nameof(Gallery), query.BookingId);
        }

        return gallery.ToResponse();
    }
}
