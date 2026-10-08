using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Persistence port for the <see cref="Booking"/> aggregate. Implemented in the Infrastructure layer.
/// </summary>
public interface IBookingRepository
{
    /// <summary>
    /// Loads a booking by identifier.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The booking, or <see langword="null"/> when it does not exist.</returns>
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Indicates whether the photographer has a tentative or confirmed booking that overlaps the slot.
    /// </summary>
    /// <param name="photographerId">Photographer identifier.</param>
    /// <param name="slot">Slot to check.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> when the slot is taken.</returns>
    Task<bool> HasOverlappingActiveBookingAsync(Guid photographerId, TimeSlot slot, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a new booking.
    /// </summary>
    /// <param name="booking">Booking to insert.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the booking is stored.</returns>
    Task AddAsync(Booking booking, CancellationToken cancellationToken);

    /// <summary>
    /// Persists the changes of an existing booking using optimistic concurrency on <see cref="AggregateRoot{TId}.Version"/>.
    /// </summary>
    /// <param name="booking">Booking to update.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the booking is stored.</returns>
    /// <exception cref="Exceptions.ConflictException">When another write changed the booking first.</exception>
    Task UpdateAsync(Booking booking, CancellationToken cancellationToken);
}
