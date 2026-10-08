using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Bookings.MarkClientAbsent;

/// <summary>
/// Marks the client as absent once the tolerance of the policy has passed (B12). Saving the booking releases its slot.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class MarkClientAbsentHandler(
    IBookingRepository repository,
    TimeProvider timeProvider) : ICommandHandler<MarkClientAbsentCommand, BookingResponse>
{
    /// <summary>
    /// Marks the client as absent.
    /// </summary>
    /// <param name="command">Booking identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated booking as seen by the photographer.</returns>
    /// <exception cref="NotFoundException">When the booking does not exist.</exception>
    public async Task<BookingResponse> HandleAsync(MarkClientAbsentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var booking = await repository.GetByIdAsync(command.BookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(Booking), command.BookingId);

        var now = timeProvider.GetUtcNow();
        booking.MarkClientAbsent(now);

        await repository.UpdateAsync(booking, cancellationToken);
        return booking.ToResponse(Actor.Photographer, now);
    }
}
