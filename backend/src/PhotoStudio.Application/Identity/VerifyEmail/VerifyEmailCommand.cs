namespace PhotoStudio.Application.Identity.VerifyEmail;

/// <summary>
/// Command for a signed-in user to prove they own their email with the code sent there.
/// </summary>
/// <param name="PhotographerId">Authenticated user, taken from the token.</param>
/// <param name="Code">The six digits the user typed.</param>
public sealed record VerifyEmailCommand(Guid PhotographerId, string Code);
