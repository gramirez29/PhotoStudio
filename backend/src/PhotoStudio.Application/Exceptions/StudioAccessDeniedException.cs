namespace PhotoStudio.Application.Exceptions;

/// <summary>
/// Thrown when the credentials are right but the account may not use the web area (it has no verified email for the second factor).
/// </summary>
/// <param name="code">Stable error code.</param>
/// <param name="message">What went wrong, without secrets.</param>
public sealed class StudioAccessDeniedException(string code, string message) : Exception(message)
{
    /// <summary>
    /// Gets the stable error code.
    /// </summary>
    public string Code { get; } = code;
}
