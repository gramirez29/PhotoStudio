namespace PhotoStudio.Application.Bookings.GetContract;

/// <summary>
/// Query for the photographer to read the contract of one of their bookings.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer; the booking must belong to them.</param>
/// <param name="BookingId">Booking identifier.</param>
public sealed record GetBookingContractQuery(Guid PhotographerId, Guid BookingId);
