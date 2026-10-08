namespace PhotoStudio.Application.Bookings.SignContractInPerson;

/// <summary>
/// Command to register a contract signed face to face or on paper (B2, photographer side).
/// </summary>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="SignerName">Name typed or written by the client.</param>
/// <param name="TemplateVersion">Contract template version.</param>
/// <param name="IsPaperContract">Whether the contract was signed on paper and photographed (external channel).</param>
public sealed record SignContractInPersonCommand(Guid BookingId, string SignerName, string TemplateVersion, bool IsPaperContract);
