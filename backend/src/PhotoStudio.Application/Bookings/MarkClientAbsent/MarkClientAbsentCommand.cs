namespace PhotoStudio.Application.Bookings.MarkClientAbsent;

/// <summary>
/// Command to record that the client did not show up to a confirmed booking.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer; the booking must belong to them.</param>
/// <param name="BookingId">Booking identifier.</param>
public sealed record MarkClientAbsentCommand(Guid PhotographerId, Guid BookingId);
