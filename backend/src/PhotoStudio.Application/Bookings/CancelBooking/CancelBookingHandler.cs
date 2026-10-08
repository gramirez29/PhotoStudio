using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Bookings.CancelBooking;

/// <summary>
/// Cancels a booking on behalf of the photographer (B8 and B10). Saving a cancelled booking releases its slot.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class CancelBookingHandler(
    IBookingRepository repository,
    TimeProvider timeProvider) : ICommandHandler<CancelBookingCommand, BookingResponse>
{
    /// <summary>
    /// Cancels the booking.
    /// </summary>
    /// <param name="command">Booking and optional reason.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated booking as seen by the photographer.</returns>
    /// <exception cref="NotFoundException">When the booking does not exist.</exception>
    public async Task<BookingResponse> HandleAsync(CancelBookingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var booking = await repository.GetByIdAsync(command.BookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(Booking), command.BookingId);

        var now = timeProvider.GetUtcNow();
        booking.Cancel(Actor.Photographer, command.Reason, now);

        await repository.UpdateAsync(booking, cancellationToken);
        return booking.ToResponse(Actor.Photographer, now);
    }
}
