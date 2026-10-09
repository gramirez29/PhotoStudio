namespace PhotoStudio.Application.Billing.GetSettlement;

/// <summary>
/// Query for the settlement of one booking.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="BookingId">Booking whose settlement is read.</param>
public sealed record GetSettlementQuery(Guid PhotographerId, Guid BookingId);
