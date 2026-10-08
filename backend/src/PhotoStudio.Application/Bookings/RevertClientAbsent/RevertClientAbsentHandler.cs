using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Bookings.RevertClientAbsent;

/// <summary>
/// Reverts a client-absent mark within the window of the policy (B13). The booking becomes confirmed again, so it takes its
/// slot back; if another booking took that slot in the meantime the reversal is rejected and the booking stays as it was.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class RevertClientAbsentHandler(
    IBookingRepository repository,
    TimeProvider timeProvider) : ICommandHandler<RevertClientAbsentCommand, BookingResponse>
{
    /// <summary>
    /// Reverts the mark.
    /// </summary>
    /// <param name="command">Booking identifier and reason.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated booking as seen by the photographer.</returns>
    /// <exception cref="NotFoundException">When the booking does not exist.</exception>
    /// <exception cref="ConflictException">When another active booking now overlaps the slot or the booking was modified meanwhile.</exception>
    public async Task<BookingResponse> HandleAsync(RevertClientAbsentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var booking = await repository.GetByIdAsync(command.BookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(Booking), command.BookingId);

        var now = timeProvider.GetUtcNow();
        booking.RevertClientAbsent(command.Reason, now);

        await repository.UpdateReservingSlotAsync(booking, cancellationToken);
        return booking.ToResponse(Actor.Photographer, now);
    }
}
