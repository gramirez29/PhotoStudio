namespace PhotoStudio.Domain.Common;

/// <summary>
/// Thrown when an operation violates a business rule of the domain.
/// </summary>
/// <param name="code">Stable, machine-readable error code (see <see cref="DomainErrorCodes"/>).</param>
/// <param name="message">Human-readable description of the violated rule.</param>
public sealed class DomainException(string code, string message) : Exception(message)
{
    /// <summary>
    /// Gets the stable, machine-readable error code that clients can rely on.
    /// </summary>
    public string Code { get; } = code;
}
