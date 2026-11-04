namespace PhotoStudio.Application.Identity.ChangeEmail;

/// <summary>
/// Command for a signed-in user who has not verified their email to replace it, typically because it was misspelled when
/// registering, and receive the code there.
/// </summary>
/// <param name="PhotographerId">Authenticated user, taken from the token.</param>
/// <param name="Email">The address the user wants, as typed.</param>
public sealed record ChangeUnverifiedEmailCommand(Guid PhotographerId, string Email);
