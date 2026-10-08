namespace PhotoStudio.Application.Bookings.CompleteBooking;

/// <summary>
/// Command to mark a confirmed booking as completed once its session has started.
/// </summary>
/// <param name="BookingId">Booking identifier.</param>
public sealed record CompleteBookingCommand(Guid BookingId);
