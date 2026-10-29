using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Portal.SignContract;

/// <summary>
/// Command for the client to sign the contract through the portal (B2, channel Portal).
/// </summary>
/// <param name="Token">Secret token taken from the URL.</param>
/// <param name="AuthorizedBookingId">Booking the portal session is valid for; it must be the booking of the link.</param>
/// <param name="TemplateVersion">Version of the contract the client read; it must still be the current one.</param>
/// <param name="SignerName">Name typed by the signer.</param>
/// <param name="IdNumber">Identity number of the signer (cédula or DIMEX).</param>
/// <param name="ParticipantCount">Number of people in the session.</param>
/// <param name="SessionLocation">Place of the session; optional.</param>
/// <param name="ImageUse">What the client allows to do with the photos.</param>
/// <param name="AcceptsTerms">Whether the client ticked the box that says they read and accept the contract.</param>
public sealed record SignPortalContractCommand(
    string Token,
    Guid AuthorizedBookingId,
    string TemplateVersion,
    string SignerName,
    string IdNumber,
    int ParticipantCount,
    string? SessionLocation,
    ImageUse ImageUse,
    bool AcceptsTerms);
