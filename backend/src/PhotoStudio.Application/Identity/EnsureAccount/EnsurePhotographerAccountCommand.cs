namespace PhotoStudio.Application.Identity.EnsureAccount;

/// <summary>
/// Command, issued at startup from the environment, to make sure the photographer account exists with the configured password.
/// </summary>
/// <param name="PhotographerId">Identifier to give the account when it is created; keeps the data of an existing tenant reachable. A new one is generated when null.</param>
/// <param name="Email">Login email.</param>
/// <param name="Password">Password in clear text; hashed before it is stored.</param>
public sealed record EnsurePhotographerAccountCommand(Guid? PhotographerId, string Email, string Password);

/// <summary>
/// What <see cref="EnsurePhotographerAccountHandler"/> did.
/// </summary>
public enum EnsureAccountOutcome
{
    /// <summary>No account used the email, so one was created.</summary>
    Created,

    /// <summary>The account existed and already had this password.</summary>
    Unchanged,

    /// <summary>The account existed with another password, which was replaced; every session of the account was revoked.</summary>
    PasswordUpdated,
}

/// <summary>
/// Outcome of ensuring the photographer account.
/// </summary>
/// <param name="PhotographerId">Identifier of the account (the existing one when it was not created).</param>
/// <param name="Outcome">What happened.</param>
public sealed record EnsureAccountResult(Guid PhotographerId, EnsureAccountOutcome Outcome);
