namespace PhotoStudio.Application.Portal.GetContract;

/// <summary>
/// Query for the client to read the contract of the booking the link opens. It needs the portal session because the text holds
/// personal data (the phone and, once signed, the identity number) that the link alone must not reveal.
/// </summary>
/// <param name="Token">Secret token taken from the URL.</param>
/// <param name="AuthorizedBookingId">Booking the portal session is valid for; it must be the booking of the link.</param>
public sealed record GetPortalContractQuery(string Token, Guid AuthorizedBookingId);
