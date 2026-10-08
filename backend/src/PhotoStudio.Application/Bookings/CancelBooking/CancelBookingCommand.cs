namespace PhotoStudio.Application.Bookings.CancelBooking;

/// <summary>
/// Command to cancel a tentative or confirmed booking, requested by the photographer.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer; the booking must belong to them.</param>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="Reason">Cancellation reason; required once the booking is confirmed.</param>
public sealed record CancelBookingCommand(Guid PhotographerId, Guid BookingId, string? Reason);
