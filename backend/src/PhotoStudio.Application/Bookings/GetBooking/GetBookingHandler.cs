using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Bookings.GetBooking;

/// <summary>
/// Reads a booking as seen by the photographer, including the actions currently allowed.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class GetBookingHandler(
    IBookingRepository repository,
    TimeProvider timeProvider) : IQueryHandler<GetBookingQuery, BookingResponse>
{
    /// <summary>
    /// Reads the booking.
    /// </summary>
    /// <param name="query">Query with the booking identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The booking response.</returns>
    /// <exception cref="NotFoundException">When the booking does not exist.</exception>
    public async Task<BookingResponse> HandleAsync(GetBookingQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var booking = await repository.GetByIdAsync(query.BookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(Booking), query.BookingId);

        return booking.ToResponse(Actor.Photographer, timeProvider.GetUtcNow());
    }
}
