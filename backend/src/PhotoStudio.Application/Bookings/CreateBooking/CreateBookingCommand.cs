using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Bookings.CreateBooking;

/// <summary>
/// An extra sold with a booking, as the photographer chose it.
/// </summary>
/// <param name="Code">Stable code of the extra.</param>
/// <param name="Name">Name of the extra.</param>
/// <param name="UnitPrice">Price of one unit when the booking is made.</param>
/// <param name="Quantity">Number of units.</param>
public sealed record BookingExtraInput(string Code, string Name, decimal UnitPrice, int Quantity);

/// <summary>
/// Command to create a tentative booking (B1).
/// </summary>
/// <param name="PhotographerId">Photographer identifier. Taken from the authenticated user once authentication exists.</param>
/// <param name="ClientName">Client name.</param>
/// <param name="ClientPhone">Client phone.</param>
/// <param name="PackageName">Name of the session (family portrait, pre-wedding...).</param>
/// <param name="PackagePrice">Price of the package alone, before extras and discount.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="SessionStart">Session start.</param>
/// <param name="SessionEnd">Session end.</param>
/// <param name="ClientEmail">Client email, optional. The client portal sends its verification codes there.</param>
/// <param name="PackageTier">Package the client booked, optional.</param>
/// <param name="Extras">Extras sold with the booking, optional.</param>
/// <param name="Discount">Discount given to the client; zero when there is none.</param>
public sealed record CreateBookingCommand(
    Guid PhotographerId,
    string ClientName,
    string ClientPhone,
    string PackageName,
    decimal PackagePrice,
    string Currency,
    DateTimeOffset SessionStart,
    DateTimeOffset SessionEnd,
    string? ClientEmail = null,
    PackageTier? PackageTier = null,
    IReadOnlyList<BookingExtraInput>? Extras = null,
    decimal Discount = 0m);
