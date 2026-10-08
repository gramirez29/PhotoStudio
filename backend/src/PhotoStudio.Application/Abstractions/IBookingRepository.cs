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
    /// Lists a photographer's bookings whose session has not ended before the given instant, earliest session first.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="endingAfter">Sessions that ended before this instant are left out.</param>
    /// <param name="limit">Maximum number of bookings to return.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The bookings, possibly empty.</returns>
    Task<IReadOnlyList<Booking>> ListByPhotographerAsync(
        Guid photographerId,
        DateTimeOffset endingAfter,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Indicates whether the photographer has a tentative or confirmed booking that overlaps the slot.
    /// </summary>
    /// <param name="photographerId">Photographer identifier.</param>
    /// <param name="slot">Slot to check.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> when the slot is taken.</returns>
    Task<bool> HasOverlappingActiveBookingAsync(Guid photographerId, TimeSlot slot, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a new booking and reserves its slot in the photographer's calendar as one atomic operation.
    /// This is the authoritative overlap check: <see cref="HasOverlappingActiveBookingAsync"/> alone cannot stop two
    /// concurrent requests from taking the same slot.
    /// </summary>
    /// <param name="booking">Booking to insert.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the booking is stored.</returns>
    /// <exception cref="Exceptions.ConflictException">When an active booking already overlaps the slot.</exception>
    Task AddAsync(Booking booking, CancellationToken cancellationToken);

    /// <summary>
    /// Persists the changes of an existing booking using optimistic concurrency on <see cref="AggregateRoot{TId}.Version"/>.
    /// When the booking is no longer tentative or confirmed, its slot is released in the same atomic operation.
    /// This method never changes the reserved slot: a booking that is still active must keep the slot it has stored, and
    /// moving it (reschedule) or reactivating it (revert client absence) goes through <see cref="UpdateReservingSlotAsync"/>.
    /// </summary>
    /// <param name="booking">Booking to update.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the booking is stored.</returns>
    /// <exception cref="Exceptions.ConflictException">When another write changed the booking first.</exception>
    /// <exception cref="InvalidOperationException">When an active booking has a different slot than the stored one.</exception>
    Task UpdateAsync(Booking booking, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a tentative or confirmed booking and reserves its current slot in the photographer's calendar as one atomic
    /// operation, releasing any slot the booking held before. The slot may overlap the booking's own previous slot, but not
    /// the slot of any other active booking. Uses optimistic concurrency on <see cref="AggregateRoot{TId}.Version"/>.
    /// </summary>
    /// <param name="booking">Booking to update; must be tentative or confirmed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the booking is stored.</returns>
    /// <exception cref="Exceptions.ConflictException">When another active booking overlaps the slot, or another write changed the booking first.</exception>
    /// <exception cref="ArgumentException">When the booking is not tentative or confirmed, because it holds no slot.</exception>
    Task UpdateReservingSlotAsync(Booking booking, CancellationToken cancellationToken);
}
