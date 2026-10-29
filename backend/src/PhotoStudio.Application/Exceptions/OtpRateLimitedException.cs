namespace PhotoStudio.Application.Exceptions;

/// <summary>
/// Thrown when a verification code was requested too soon after the previous one, or too many times in an hour.
/// </summary>
/// <param name="retryAfter">Time left until another code can be requested.</param>
/// <param name="code">Stable error code: the portal one by default, or the one of the account email verification.</param>
public sealed class OtpRateLimitedException(TimeSpan retryAfter, string code = ApplicationErrorCodes.OtpRateLimited) : Exception("A verification code was requested too many times. Wait a moment and try again.")
{
    /// <summary>
    /// Gets the stable error code.
    /// </summary>
    public string Code { get; } = code;

    /// <summary>
    /// Gets the time left until another code can be requested.
    /// </summary>
    public TimeSpan RetryAfter { get; } = retryAfter;
}
