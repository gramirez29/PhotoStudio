namespace PhotoStudio.Application.Bookings.GetBooking;

/// <summary>
/// Query to read a booking as seen by the photographer.
/// </summary>
/// <param name="BookingId">Booking identifier.</param>
public sealed record GetBookingQuery(Guid BookingId);
