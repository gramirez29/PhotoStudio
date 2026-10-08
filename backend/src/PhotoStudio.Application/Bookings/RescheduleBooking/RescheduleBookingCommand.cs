namespace PhotoStudio.Application.Bookings.RescheduleBooking;

/// <summary>
/// Command to move a confirmed booking to another slot, requested by the photographer.
/// </summary>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="NewStart">New session start, with offset.</param>
/// <param name="NewEnd">New session end, with offset.</param>
public sealed record RescheduleBookingCommand(Guid BookingId, DateTimeOffset NewStart, DateTimeOffset NewEnd);
