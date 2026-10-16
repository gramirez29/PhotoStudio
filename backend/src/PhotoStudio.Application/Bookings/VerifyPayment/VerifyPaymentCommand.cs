namespace PhotoStudio.Application.Bookings.VerifyPayment;

/// <summary>
/// Command for the photographer to accept a payment proof sent by the client (B4). It can confirm the booking.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer; the booking must belong to them.</param>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="PaymentId">Payment waiting for verification.</param>
public sealed record VerifyPaymentCommand(Guid PhotographerId, Guid BookingId, Guid PaymentId);
