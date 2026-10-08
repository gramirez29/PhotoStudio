using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Bookings.CompleteBooking;

/// <summary>
/// Marks a booking as completed (B11). Saving a completed booking releases its slot; the gallery module will react to
/// the <c>SessionCompleted</c> event once the outbox exists.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class CompleteBookingHandler(
    IBookingRepository repository,
    TimeProvider timeProvider) : ICommandHandler<CompleteBookingCommand, BookingResponse>
{
    /// <summary>
    /// Completes the booking.
    /// </summary>
    /// <param name="command">Booking identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated booking as seen by the photographer.</returns>
    /// <exception cref="NotFoundException">When the booking does not exist.</exception>
    public async Task<BookingResponse> HandleAsync(CompleteBookingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var booking = await repository.GetByIdAsync(command.BookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(Booking), command.BookingId);

        var now = timeProvider.GetUtcNow();
        booking.Complete(now);

        await repository.UpdateAsync(booking, cancellationToken);
        return booking.ToResponse(Actor.Photographer, now);
    }
}
