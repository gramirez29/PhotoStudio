using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.Portal.SubmitProof;

/// <summary>
/// Registers a SINPE Móvil proof sent by the client. The portal session must be the one issued for the booking of the link,
/// so a session obtained for one booking cannot act on another. The amount cannot exceed what is still owed after counting
/// the proofs already waiting for verification.
/// </summary>
/// <param name="bookings">Booking repository.</param>
/// <param name="links">Link repository.</param>
/// <param name="tokens">Hashes the token taken from the URL.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class SubmitPortalPaymentProofHandler(
    IBookingRepository bookings,
    IBookingLinkRepository links,
    IRefreshTokenGenerator tokens,
    TimeProvider timeProvider) : ICommandHandler<SubmitPortalPaymentProofCommand, PortalBookingResponse>
{
    /// <summary>
    /// Registers the proof.
    /// </summary>
    /// <param name="command">Proof data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The booking as the client sees it after the proof.</returns>
    /// <exception cref="NotFoundException">When the token does not open the booking the session was issued for.</exception>
    /// <exception cref="DomainException">When the amount, the reference or the booking status does not allow the proof.</exception>
    /// <exception cref="ConflictException">When another write changed the booking first.</exception>
    public async Task<PortalBookingResponse> HandleAsync(SubmitPortalPaymentProofCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var link = await links.ResolveAsync(tokens, command.Token, now, cancellationToken);
        if (link.BookingId != command.AuthorizedBookingId)
        {
            throw new NotFoundException(nameof(Booking), Guid.Empty);
        }

        var booking = await bookings.GetBookingAsync(link, cancellationToken);
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The idempotency key is required.");
        }

        var amount = Money.Create(command.Amount, booking.PackagePrice.Currency);
        var alreadyReported = booking.Payments
            .Where(payment => payment.Status == PaymentStatus.PendingVerification && payment.IdempotencyKey != command.IdempotencyKey.Trim())
            .Sum(payment => payment.Amount.Amount);
        if (amount.Amount > booking.Balance.Amount - alreadyReported)
        {
            throw new DomainException(DomainErrorCodes.InvalidAmount, "The amount is more than what is still owed.");
        }

        var paymentCountBefore = booking.Payments.Count;
        booking.SubmitPaymentProof(Guid.CreateVersion7(now), amount, PaymentMethod.SinpeMovil, command.IdempotencyKey, now, command.Reference);

        // A repeated idempotency key returns the original proof without changes, so there is nothing to persist.
        if (booking.Payments.Count != paymentCountBefore)
        {
            await bookings.UpdateAsync(booking, cancellationToken);
        }

        return booking.ToPortalResponse();
    }
}
