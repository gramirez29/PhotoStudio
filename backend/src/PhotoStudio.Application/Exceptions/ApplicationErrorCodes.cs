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

    /// <summary>Another user already has that username.</summary>
    public const string UsernameTaken = "user.username_taken";

    /// <summary>Another user already has that email.</summary>
    public const string EmailTaken = "user.email_taken";

    /// <summary>Creating accounts is turned off in this environment.</summary>
    public const string RegistrationDisabled = "auth.registration_disabled";

    /// <summary>Too many consecutive failed logins; the account is temporarily locked.</summary>
    public const string AccountLocked = "auth.account_locked";

    /// <summary>The current password sent to change the password is not correct. It is not a 401: the user's session is valid.</summary>
    public const string InvalidCurrentPassword = "user.invalid_current_password";

    /// <summary>The refresh token is unknown, expired or was already used; the user must sign in again.</summary>
    public const string InvalidRefreshToken = "auth.invalid_refresh_token";

    /// <summary>The email could not be sent (provider not configured or it refused the message).</summary>
    public const string EmailDeliveryFailed = "email.delivery_failed";

    /// <summary>A verification code was requested too soon or too many times.</summary>
    public const string OtpRateLimited = "portal.otp_rate_limited";

    /// <summary>The booking has no client email, so no verification code can be sent.</summary>
    public const string EmailMissing = "portal.email_missing";

    /// <summary>The verification code is wrong, expired or spent (the response never says which).</summary>
    public const string InvalidCode = "portal.invalid_code";

    /// <summary>A code to verify the email of an account was requested too soon or too many times.</summary>
    public const string EmailCodeRateLimited = "auth.email_code_rate_limited";

    /// <summary>The code to verify the email of an account is wrong, expired or spent (the response never says which).</summary>
    public const string InvalidEmailCode = "auth.invalid_email_code";

    /// <summary>The email of the account was already verified, so there is nothing to send.</summary>
    public const string EmailAlreadyVerified = "auth.email_already_verified";

    /// <summary>The account has no email (it was created before the email existed), so no code can be sent.</summary>
    public const string AccountEmailMissing = "auth.email_missing";
}
