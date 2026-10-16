namespace PhotoStudio.Application.Portal.RequestCode;

/// <summary>
/// Command to send a verification code to the email of the booking's client.
/// </summary>
/// <param name="Token">Secret token taken from the URL.</param>
public sealed record RequestPortalCodeCommand(string Token);
