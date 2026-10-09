using PhotoStudio.Domain.Billing;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Persistence port of the settlements of bookings. There is at most one settlement per booking.
/// </summary>
public interface ISettlementRepository
{
    /// <summary>
    /// Finds the settlement of a booking.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The settlement, or <see langword="null"/> when the booking has none.</returns>
    Task<BookingSettlement?> GetByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a settlement.
    /// </summary>
    /// <param name="settlement">Settlement to add.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> when it was added; <see langword="false"/> when the booking already had one
    /// (another event created it first), in which case the caller reads it and applies the outcome to it.</returns>
    Task<bool> TryAddAsync(BookingSettlement settlement, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a settlement with optimistic concurrency.
    /// </summary>
    /// <param name="settlement">Settlement to save.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the settlement is saved.</returns>
    /// <exception cref="Exceptions.ConflictException">When it was changed by another request.</exception>
    Task UpdateAsync(BookingSettlement settlement, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the settlements of a photographer whose refund is still pending, the oldest first.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant).</param>
    /// <param name="limit">Maximum number of settlements.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The settlements.</returns>
    Task<IReadOnlyList<BookingSettlement>> ListPendingRefundsAsync(Guid photographerId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Counts the refunds of a photographer that are still pending.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of pending refunds.</returns>
    Task<int> CountPendingRefundsAsync(Guid photographerId, CancellationToken cancellationToken);
}
