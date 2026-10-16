namespace PhotoStudio.Application.Identity.ChangePassword;

/// <summary>
/// Command to change the password of the signed-in user.
/// </summary>
/// <param name="PhotographerId">User that changes the password; always taken from the access token, never from the body.</param>
/// <param name="CurrentPassword">Password the user has now, in clear text.</param>
/// <param name="NewPassword">Password the user wants, in clear text.</param>
public sealed record ChangePasswordCommand(Guid PhotographerId, string CurrentPassword, string NewPassword);
