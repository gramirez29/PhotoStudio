namespace PhotoStudio.Application.Portal.GetBooking;

/// <summary>
/// Query for the booking a link opens.
/// </summary>
/// <param name="Token">Secret token taken from the URL.</param>
public sealed record GetPortalBookingQuery(string Token);
