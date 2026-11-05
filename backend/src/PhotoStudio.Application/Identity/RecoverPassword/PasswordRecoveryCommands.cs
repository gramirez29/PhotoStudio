namespace PhotoStudio.Application.Identity.RecoverPassword;

/// <summary>
/// Command to ask for the code that lets a user who forgot their password choose a new one. Nobody is signed in, so the user is
/// named by what they remember.
/// </summary>
/// <param name="Identifier">Username or email of the account, as typed.</param>
public sealed record RequestPasswordResetCommand(string Identifier);

/// <summary>
/// Command to choose a new password with the code that was sent to the email of the account.
/// </summary>
/// <param name="Identifier">Username or email of the account, as typed.</param>
/// <param name="Code">The six digits the user typed.</param>
/// <param name="NewPassword">The password the user wants: 8 to 128 characters.</param>
public sealed record ConfirmPasswordResetCommand(string Identifier, string Code, string NewPassword);

/// <summary>
/// Answer to a request for a code. It is the same whether or not the account exists, so it cannot be used to find out which
/// usernames or emails are registered.
/// </summary>
public sealed record PasswordResetRequestedResponse;

/// <summary>
/// Confirmation that the password was changed. The user signs in again with the new one: every session was closed.
/// </summary>
public sealed record PasswordResetResponse;
