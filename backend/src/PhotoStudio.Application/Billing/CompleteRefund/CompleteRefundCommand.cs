using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Billing.CompleteRefund;

/// <summary>
/// Command that records that the photographer gave the money of a refund back to the client.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="BookingId">Booking whose refund was given back.</param>
/// <param name="Method">How the money was given back.</param>
/// <param name="Note">Optional note, for example the SINPE reference.</param>
public sealed record CompleteRefundCommand(Guid PhotographerId, Guid BookingId, PaymentMethod Method, string? Note);
