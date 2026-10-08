namespace PhotoStudio.Application.Bookings.GetBooking;

/// <summary>
/// Query to read a booking as seen by the photographer.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer; the booking must belong to them.</param>
/// <param name="BookingId">Booking identifier.</param>
public sealed record GetBookingQuery(Guid PhotographerId, Guid BookingId);
