namespace PhotoStudio.Application.Identity;

/// <summary>
/// Whether new accounts can be created, configured from the environment. Turning it off lets an operator close sign-up
/// once the accounts that are needed exist.
/// </summary>
/// <param name="Enabled"><see langword="true"/> when anyone can register.</param>
public sealed record RegistrationSettings(bool Enabled);
