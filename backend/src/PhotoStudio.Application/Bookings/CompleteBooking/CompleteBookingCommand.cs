namespace PhotoStudio.Application.Bookings.CompleteBooking;

/// <summary>
/// Command to mark a confirmed booking as completed once its session has started.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer; the booking must belong to them.</param>
/// <param name="BookingId">Booking identifier.</param>
public sealed record CompleteBookingCommand(Guid PhotographerId, Guid BookingId);
