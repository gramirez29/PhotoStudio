namespace PhotoStudio.Application.Exceptions;

/// <summary>
/// Thrown when the credentials or the refresh token presented by the app are not accepted. The message never says which
/// part was wrong (unknown email or wrong password), so it cannot be used to discover which emails have an account.
/// </summary>
/// <param name="code">Stable error code (see <see cref="ApplicationErrorCodes"/>).</param>
/// <param name="message">Human-readable description.</param>
public sealed class AuthenticationFailedException(string code, string message) : Exception(message)
{
    /// <summary>
    /// Gets the stable error code.
    /// </summary>
    public string Code { get; } = code;
}
