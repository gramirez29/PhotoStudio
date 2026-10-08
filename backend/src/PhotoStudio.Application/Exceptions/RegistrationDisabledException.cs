namespace PhotoStudio.Application.Exceptions;

/// <summary>
/// Thrown when someone tries to create an account while sign-up is turned off in this environment.
/// </summary>
public sealed class RegistrationDisabledException() : Exception("Creating accounts is turned off.")
{
    /// <summary>
    /// Gets the stable error code.
    /// </summary>
    public string Code => ApplicationErrorCodes.RegistrationDisabled;
}
