using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.Bookings.RescheduleBooking;

/// <summary>
/// Moves a confirmed booking to another slot on behalf of the photographer (B9). The domain validates the transition;
/// the repository reserves the new slot and releases the old one atomically, so two bookings can never end up overlapping.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class RescheduleBookingHandler(
    IBookingRepository repository,
    TimeProvider timeProvider) : ICommandHandler<RescheduleBookingCommand, BookingResponse>
{
    /// <summary>
    /// Reschedules the booking.
    /// </summary>
    /// <param name="command">Booking and new slot.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated booking as seen by the photographer.</returns>
    /// <exception cref="NotFoundException">When the booking does not exist.</exception>
    /// <exception cref="ConflictException">When the new slot overlaps another active booking or the booking was modified meanwhile.</exception>
    public async Task<BookingResponse> HandleAsync(RescheduleBookingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var booking = await repository.GetOwnedAsync(command.BookingId, command.PhotographerId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        booking.Reschedule(TimeSlot.Create(command.NewStart, command.NewEnd), Actor.Photographer, now);

        await repository.UpdateReservingSlotAsync(booking, cancellationToken);
        return booking.ToResponse(Actor.Photographer, now);
    }
}
