namespace PhotoStudio.Application.Bookings.RevertClientAbsent;

/// <summary>
/// Command to undo a client-absent mark made by mistake.
/// </summary>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="Reason">Why the mark is reverted; required.</param>
public sealed record RevertClientAbsentCommand(Guid BookingId, string Reason);
