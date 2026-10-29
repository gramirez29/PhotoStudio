using PhotoStudio.Application.Bookings.CreateBooking;
using PhotoStudio.Domain.Bookings;

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
/// <param name="PackagePrice">Price of the package alone, in the currency of the booking.</param>
/// <param name="PackageTier">Package the client booked; <see langword="null"/> leaves it as it is.</param>
/// <param name="Extras">Extras sold with the booking as they should be now (an empty list removes them); <see langword="null"/> leaves them as they are.</param>
/// <param name="Discount">Discount given to the client as it should be now; <see langword="null"/> leaves it as it is.</param>
public sealed record EditBookingDetailsCommand(
    Guid PhotographerId,
    Guid BookingId,
    string ClientName,
    string ClientPhone,
    string? ClientEmail,
    string PackageName,
    decimal PackagePrice,
    PackageTier? PackageTier = null,
    IReadOnlyList<BookingExtraInput>? Extras = null,
    decimal? Discount = null);
