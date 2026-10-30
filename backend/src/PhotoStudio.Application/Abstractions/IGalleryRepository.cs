using PhotoStudio.Application.Pagination;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Persistence port of the galleries. There is at most one gallery per booking.
/// </summary>
public interface IGalleryRepository
{
    /// <summary>
    /// Finds a gallery by its identifier.
    /// </summary>
    /// <param name="id">Gallery identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery, or <see langword="null"/> when it does not exist.</returns>
    Task<Gallery?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the gallery of a booking.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery, or <see langword="null"/> when the booking has none.</returns>
    Task<Gallery?> GetByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a gallery.
    /// </summary>
    /// <param name="gallery">Gallery to add.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> when it was added; <see langword="false"/> when the booking already had one (the event that
    /// creates it was delivered twice), which is not an error.</returns>
    Task<bool> TryAddAsync(Gallery gallery, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a gallery with optimistic concurrency.
    /// </summary>
    /// <param name="gallery">Gallery to save.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the gallery is saved.</returns>
    /// <exception cref="Exceptions.ConflictException">When it was changed by another request.</exception>
    Task UpdateAsync(Gallery gallery, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the galleries of a photographer, the most recently created first.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="after">The last gallery the caller already has; the list continues right after it. <see langword="null"/> starts at the newest.</param>
    /// <param name="limit">Maximum number of galleries to return.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The galleries, possibly empty.</returns>
    Task<IReadOnlyList<Gallery>> ListByPhotographerAsync(Guid photographerId, PageCursor? after, int limit, CancellationToken cancellationToken);
}
