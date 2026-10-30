namespace PhotoStudio.Application.Identity.Studio;

/// <summary>
/// The signed-in user of the web area, as the page may show them. The email is masked: the web area shows less than the app.
/// </summary>
/// <param name="PhotographerId">Identifier of the user (tenant).</param>
/// <param name="Username">Username.</param>
/// <param name="Name">Name.</param>
/// <param name="MaskedEmail">Email with the middle hidden, for example <c>g***@gmail.com</c>.</param>
public sealed record StudioUserSummary(Guid PhotographerId, string Username, string Name, string MaskedEmail);

/// <summary>
/// A session that was just started. The secret goes to the browser in a cookie and is never stored.
/// </summary>
/// <param name="Token">Secret of the session cookie.</param>
/// <param name="ExpiresAt">Instant the session ends however much it is used (UTC).</param>
/// <param name="User">Who signed in.</param>
public sealed record StudioIssuedSession(string Token, DateTimeOffset ExpiresAt, StudioUserSummary User);

/// <summary>
/// A remembered browser whose cookie has to be set. The secret is never stored.
/// </summary>
/// <param name="Token">Secret of the device cookie.</param>
/// <param name="ExpiresAt">Instant the browser is forgotten (UTC).</param>
public sealed record StudioIssuedDevice(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// How a web sign-in with the right password ended.
/// </summary>
public abstract record StudioLoginResult;

/// <summary>
/// The sign-in is complete: the browser was remembered, so the code was not asked.
/// </summary>
/// <param name="Session">The new session.</param>
/// <param name="Device">The remembered browser with its new secret (it changes at every use).</param>
public sealed record StudioSignedIn(StudioIssuedSession Session, StudioIssuedDevice Device) : StudioLoginResult;

/// <summary>
/// A code was sent to the email of the user and has to come back to finish the sign-in.
/// </summary>
/// <param name="ChallengeToken">Secret of the challenge cookie; it ties the code to this browser.</param>
/// <param name="MaskedEmail">Where the code went, with the middle hidden.</param>
/// <param name="ExpiresAt">Instant the code stops being accepted (UTC).</param>
public sealed record StudioCodeRequired(string ChallengeToken, string MaskedEmail, DateTimeOffset ExpiresAt) : StudioLoginResult;

/// <summary>
/// The outcome of a correct code.
/// </summary>
/// <param name="Session">The new session.</param>
/// <param name="Device">The remembered browser, when the user asked to remember it.</param>
public sealed record StudioVerifyResult(StudioIssuedSession Session, StudioIssuedDevice? Device);

/// <summary>
/// Where a new code went.
/// </summary>
/// <param name="MaskedEmail">Where the code went, with the middle hidden.</param>
/// <param name="ExpiresAt">Instant the code stops being accepted (UTC).</param>
public sealed record StudioCodeSentResponse(string MaskedEmail, DateTimeOffset ExpiresAt);

/// <summary>
/// Command to start a web sign-in.
/// </summary>
/// <param name="Username">Username as typed.</param>
/// <param name="Password">Password as typed.</param>
/// <param name="DeviceToken">Secret of the device cookie the browser sent, if it sent one.</param>
public sealed record StudioLoginCommand(string? Username, string? Password, string? DeviceToken);

/// <summary>
/// Command to finish a web sign-in with the code from the email.
/// </summary>
/// <param name="ChallengeToken">Secret of the challenge cookie the browser sent.</param>
/// <param name="Code">The code as typed.</param>
/// <param name="RememberDevice">Whether to skip the code in this browser for a while.</param>
public sealed record StudioVerifyCodeCommand(string? ChallengeToken, string? Code, bool RememberDevice);

/// <summary>
/// Command to send a new code to the email of a sign-in that is waiting for it.
/// </summary>
/// <param name="ChallengeToken">Secret of the challenge cookie the browser sent.</param>
public sealed record StudioResendCodeCommand(string? ChallengeToken);

/// <summary>
/// Command to close the session of a browser.
/// </summary>
/// <param name="SessionToken">Secret of the session cookie.</param>
/// <param name="DeviceToken">Secret of the device cookie, if the browser has one.</param>
/// <param name="ForgetDevice">Whether to also forget this browser, so the next sign-in asks for the code again.</param>
public sealed record StudioLogoutCommand(string? SessionToken, string? DeviceToken, bool ForgetDevice);
