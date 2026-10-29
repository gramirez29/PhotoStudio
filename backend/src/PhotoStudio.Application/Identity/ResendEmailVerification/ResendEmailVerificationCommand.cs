namespace PhotoStudio.Application.Identity.ResendEmailVerification;

/// <summary>
/// Command for a signed-in user to ask for a new code to verify their email.
/// </summary>
/// <param name="PhotographerId">Authenticated user, taken from the token.</param>
public sealed record ResendEmailVerificationCommand(Guid PhotographerId);

/// <summary>
/// Confirmation that a code to verify the email was sent.
/// </summary>
/// <param name="MaskedEmail">Address the code went to, partly hidden, so the user knows where to look.</param>
/// <param name="ExpiresAt">Instant the code stops being accepted (UTC).</param>
public sealed record EmailVerificationSentResponse(string MaskedEmail, DateTimeOffset ExpiresAt);
