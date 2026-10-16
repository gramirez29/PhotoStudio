using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.Bookings.RejectPayment;

/// <summary>
/// Rejects a payment proof the photographer could not match with a transfer. The payment is kept with its reason, never
/// deleted, and does not count toward the balance. With no proof waiting any more, an unconfirmed booking can expire.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class RejectPaymentHandler(
    IBookingRepository repository,
    TimeProvider timeProvider) : ICommandHandler<RejectPaymentCommand, BookingResponse>
{
    /// <summary>
    /// Rejects the payment.
    /// </summary>
    /// <param name="command">Booking, payment and reason.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated booking as seen by the photographer.</returns>
    /// <exception cref="NotFoundException">When the booking does not exist or belongs to another photographer.</exception>
    /// <exception cref="DomainException">When the reason is empty, or the payment does not exist or is not waiting for verification.</exception>
    public async Task<BookingResponse> HandleAsync(RejectPaymentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var booking = await repository.GetOwnedAsync(command.BookingId, command.PhotographerId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        booking.RejectPayment(command.PaymentId, command.Reason, now);
        await repository.UpdateAsync(booking, cancellationToken);

        return booking.ToResponse(Actor.Photographer, now);
    }
}
