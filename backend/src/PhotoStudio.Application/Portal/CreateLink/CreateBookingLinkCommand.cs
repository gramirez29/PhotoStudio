namespace PhotoStudio.Application.Portal.CreateLink;

/// <summary>
/// Command to create the link a client opens to see and act on a booking. Creating it again replaces the previous link.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer; the booking must belong to them.</param>
/// <param name="BookingId">Booking identifier.</param>
public sealed record CreateBookingLinkCommand(Guid PhotographerId, Guid BookingId);
