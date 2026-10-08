namespace PhotoStudio.Application.Identity.Register;

/// <summary>
/// Command to create a user account and sign it in.
/// </summary>
/// <param name="Username">Login name chosen by the user.</param>
/// <param name="Email">Email address of the user.</param>
/// <param name="Password">Password chosen by the user, in clear text; hashed before it is stored.</param>
/// <param name="Name">Display name.</param>
/// <param name="Phone">Phone number.</param>
public sealed record RegisterUserCommand(string Username, string Email, string Password, string Name, string Phone);
