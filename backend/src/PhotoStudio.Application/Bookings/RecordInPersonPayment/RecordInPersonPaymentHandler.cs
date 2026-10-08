using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.Bookings.RecordInPersonPayment;

/// <summary>
/// Records a payment received face to face; confirms the booking when the contract is already signed.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class RecordInPersonPaymentHandler(
    IBookingRepository repository,
    TimeProvider timeProvider) : ICommandHandler<RecordInPersonPaymentCommand, BookingResponse>
{
    /// <summary>
    /// Records the payment.
    /// </summary>
    /// <param name="command">Payment data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated booking as seen by the photographer.</returns>
    /// <exception cref="NotFoundException">When the booking does not exist.</exception>
    public async Task<BookingResponse> HandleAsync(RecordInPersonPaymentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var booking = await repository.GetByIdAsync(command.BookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(Booking), command.BookingId);

        var now = timeProvider.GetUtcNow();
        var paymentCountBefore = booking.Payments.Count;

        booking.RecordInPersonPayment(
            Guid.CreateVersion7(now),
            Money.Create(command.Amount, command.Currency),
            command.Method,
            command.IdempotencyKey,
            now);

        // A repeated idempotency key returns the original payment without changes, so there is nothing to persist.
        if (booking.Payments.Count != paymentCountBefore)
        {
            await repository.UpdateAsync(booking, cancellationToken);
        }

        return booking.ToResponse(Actor.Photographer, now);
    }
}
