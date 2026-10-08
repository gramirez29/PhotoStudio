namespace PhotoStudio.Application.Identity.Login;

/// <summary>
/// Command to sign a user in with username and password.
/// </summary>
/// <param name="Username">Username typed by the user.</param>
/// <param name="Password">Password typed by the user.</param>
public sealed record LoginCommand(string Username, string Password);
