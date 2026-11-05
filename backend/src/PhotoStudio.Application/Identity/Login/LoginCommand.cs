namespace PhotoStudio.Application.Identity.Login;

/// <summary>
/// Command to sign a user in with username and password.
/// </summary>
/// <param name="Username">Username typed by the user.</param>
/// <param name="Password">Password typed by the user.</param>
/// <param name="ClientAddress">Network address the request came from, if known; the failed guesses are counted per address.</param>
public sealed record LoginCommand(string Username, string Password, string? ClientAddress = null);
