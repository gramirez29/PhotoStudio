namespace PhotoStudio.Application.Bookings.RejectPayment;

/// <summary>
/// Command for the photographer to reject a payment proof sent by the client (B4). The proof stays as evidence.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer; the booking must belong to them.</param>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="PaymentId">Payment waiting for verification.</param>
/// <param name="Reason">Why the proof is rejected; required.</param>
public sealed record RejectPaymentCommand(Guid PhotographerId, Guid BookingId, Guid PaymentId, string Reason);
