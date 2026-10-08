using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Bookings;

/// <summary>
/// Tenant-aware lookups on <see cref="IBookingRepository"/>. Every use case that receives a booking identifier from a client
/// loads it through here, so a photographer can never read or change the booking of another one.
/// </summary>
internal static class BookingRepositoryExtensions
{
    /// <summary>
    /// Loads a booking that belongs to the photographer. A booking of another photographer is reported exactly like a
    /// missing one, so the response never reveals that the identifier exists.
    /// </summary>
    /// <param name="repository">Booking repository.</param>
    /// <param name="bookingId">Booking identifier.</param>
    /// <param name="photographerId">Authenticated photographer (tenant).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The booking.</returns>
    /// <exception cref="NotFoundException">When the booking does not exist or belongs to another photographer.</exception>
    public static async Task<Booking> GetOwnedAsync(
        this IBookingRepository repository,
        Guid bookingId,
        Guid photographerId,
        CancellationToken cancellationToken)
    {
        var booking = await repository.GetByIdAsync(bookingId, cancellationToken);
        if (booking is null || booking.PhotographerId != photographerId)
        {
            throw new NotFoundException(nameof(Booking), bookingId);
        }

        return booking;
    }
}
