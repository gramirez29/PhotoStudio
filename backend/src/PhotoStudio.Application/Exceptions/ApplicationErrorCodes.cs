namespace PhotoStudio.Application.Exceptions;

/// <summary>
/// Stable error codes raised by the application layer.
/// </summary>
public static class ApplicationErrorCodes
{
    /// <summary>The photographer already has an active booking that overlaps the requested slot.</summary>
    public const string SlotUnavailable = "booking.slot_unavailable";

    /// <summary>Another request modified the resource first; the client should reload and retry.</summary>
    public const string ConcurrencyConflict = "concurrency.conflict";

    /// <summary>The requested resource does not exist.</summary>
    public const string NotFound = "resource.not_found";

    /// <summary>The email or the password is not correct (the response never says which).</summary>
    public const string InvalidCredentials = "auth.invalid_credentials";

    /// <summary>An account with the same email or identifier already exists.</summary>
    public const string AccountAlreadyExists = "account.already_exists";

    /// <summary>Too many consecutive failed logins; the account is temporarily locked.</summary>
    public const string AccountLocked = "auth.account_locked";

    /// <summary>The refresh token is unknown, expired or was already used; the user must sign in again.</summary>
    public const string InvalidRefreshToken = "auth.invalid_refresh_token";
}
