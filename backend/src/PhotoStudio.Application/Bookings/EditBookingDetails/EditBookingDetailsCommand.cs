namespace PhotoStudio.Application.Bookings.EditBookingDetails;

/// <summary>
/// Command to correct the contact data of the client of a booking and, while it is still possible, its package and price. It
/// carries every editable value, so what did not change is simply the same as before.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer; the booking must belong to them.</param>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="ClientName">Name of the client.</param>
/// <param name="ClientPhone">Phone of the client.</param>
/// <param name="ClientEmail">Email of the client, or <see langword="null"/> or blank for none. The client portal sends its codes there.</param>
/// <param name="PackageName">Name of the package.</param>
/// <param name="PackagePrice">Price of the package alone, in the currency of the booking. The extras and the discount of the booking do not change.</param>
public sealed record EditBookingDetailsCommand(
    Guid PhotographerId,
    Guid BookingId,
    string ClientName,
    string ClientPhone,
    string? ClientEmail,
    string PackageName,
    decimal PackagePrice);
