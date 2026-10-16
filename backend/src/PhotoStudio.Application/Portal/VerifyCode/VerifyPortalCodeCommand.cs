namespace PhotoStudio.Application.Portal.VerifyCode;

/// <summary>
/// Command to exchange the verification code the client received for a portal session.
/// </summary>
/// <param name="Token">Secret token taken from the URL.</param>
/// <param name="Code">Code typed by the client.</param>
public sealed record VerifyPortalCodeCommand(string Token, string Code);
