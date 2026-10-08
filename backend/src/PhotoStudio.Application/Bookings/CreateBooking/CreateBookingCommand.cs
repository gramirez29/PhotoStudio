namespace PhotoStudio.Application.Bookings.CreateBooking;

/// <summary>
/// Command to create a tentative booking (B1).
/// </summary>
/// <param name="PhotographerId">Photographer identifier. Taken from the authenticated user once authentication exists.</param>
/// <param name="ClientName">Client name.</param>
/// <param name="ClientPhone">Client phone.</param>
/// <param name="PackageName">Package name.</param>
/// <param name="PackagePrice">Package price.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="SessionStart">Session start.</param>
/// <param name="SessionEnd">Session end.</param>
public sealed record CreateBookingCommand(
    Guid PhotographerId,
    string ClientName,
    string ClientPhone,
    string PackageName,
    decimal PackagePrice,
    string Currency,
    DateTimeOffset SessionStart,
    DateTimeOffset SessionEnd);
