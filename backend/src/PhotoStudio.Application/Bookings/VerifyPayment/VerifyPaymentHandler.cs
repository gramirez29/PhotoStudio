using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.Bookings.VerifyPayment;

/// <summary>
/// Accepts a payment proof after the photographer checked it against their bank. The payment counts toward the deposit and
/// the balance from now on, and the booking is confirmed when the contract is also signed and the deposit is covered.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class VerifyPaymentHandler(
    IBookingRepository repository,
    TimeProvider timeProvider) : ICommandHandler<VerifyPaymentCommand, BookingResponse>
{
    /// <summary>
    /// Verifies the payment.
    /// </summary>
    /// <param name="command">Booking and payment.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated booking as seen by the photographer.</returns>
    /// <exception cref="NotFoundException">When the booking does not exist or belongs to another photographer.</exception>
    /// <exception cref="DomainException">When the payment does not exist or is not waiting for verification.</exception>
    public async Task<BookingResponse> HandleAsync(VerifyPaymentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var booking = await repository.GetOwnedAsync(command.BookingId, command.PhotographerId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        booking.VerifyPayment(command.PaymentId, now);
        await repository.UpdateAsync(booking, cancellationToken);

        return booking.ToResponse(Actor.Photographer, now);
    }
}
