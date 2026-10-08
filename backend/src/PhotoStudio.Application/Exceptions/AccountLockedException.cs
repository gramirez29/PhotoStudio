namespace PhotoStudio.Application.Exceptions;

/// <summary>
/// Thrown when a login is refused because too many consecutive attempts failed and the account is temporarily locked.
/// </summary>
/// <param name="retryAfter">Time left until the account accepts logins again.</param>
public sealed class AccountLockedException(TimeSpan retryAfter)
    : Exception("Too many failed login attempts. The account is temporarily locked.")
{
    /// <summary>
    /// Gets the stable error code.
    /// </summary>
    public string Code => ApplicationErrorCodes.AccountLocked;

    /// <summary>
    /// Gets the time left until the account accepts logins again.
    /// </summary>
    public TimeSpan RetryAfter { get; } = retryAfter;
}
