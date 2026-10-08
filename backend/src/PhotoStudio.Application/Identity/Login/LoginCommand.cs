namespace PhotoStudio.Application.Identity.Login;

/// <summary>
/// Command to sign a photographer in with email and password.
/// </summary>
/// <param name="Email">Email typed by the user.</param>
/// <param name="Password">Password typed by the user.</param>
public sealed record LoginCommand(string Email, string Password);
