using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Bookings.SignContractInPerson;

/// <summary>
/// What the client declares when they sign the contract on the photographer's phone: the blanks of the contract that only the
/// client knows. They are the same ones the client gives through the portal.
/// </summary>
/// <param name="IdNumber">Identity number of the signer (cédula or DIMEX).</param>
/// <param name="ParticipantCount">Number of people in the session.</param>
/// <param name="SessionLocation">Place of the session; optional.</param>
/// <param name="ImageUse">What the client allows to do with the photos.</param>
public sealed record InPersonDeclaration(string IdNumber, int ParticipantCount, string? SessionLocation, ImageUse ImageUse);

/// <summary>
/// Command to register a contract signed face to face or on paper (B2, photographer side). Face to face, the client reads the
/// contract on the photographer's phone and signs it there, so the declaration is given and the exact text signed is kept, like
/// in the portal. A contract on paper has no text to keep and takes no declaration.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer; the booking must belong to them.</param>
/// <param name="BookingId">Booking identifier.</param>
/// <param name="SignerName">Name typed or written by the client.</param>
/// <param name="TemplateVersion">Contract template version. With a declaration it must be the current one: the version the client read.</param>
/// <param name="IsPaperContract">Whether the contract was signed on paper and photographed (external channel).</param>
/// <param name="Declaration">What the client declared, when they signed on the phone; ignored for a contract on paper.</param>
public sealed record SignContractInPersonCommand(
    Guid PhotographerId,
    Guid BookingId,
    string SignerName,
    string TemplateVersion,
    bool IsPaperContract,
    InPersonDeclaration? Declaration = null);
