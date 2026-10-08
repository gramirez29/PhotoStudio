namespace PhotoStudio.Application.Exceptions;

/// <summary>
/// Thrown when a request conflicts with the current state of the system (taken slot, concurrent write).
/// </summary>
/// <param name="code">Stable error code (see <see cref="ApplicationErrorCodes"/>).</param>
/// <param name="message">Human-readable description of the conflict.</param>
public sealed class ConflictException(string code, string message) : Exception(message)
{
    /// <summary>
    /// Gets the stable error code.
    /// </summary>
    public string Code { get; } = code;
}
