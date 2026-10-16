namespace PhotoStudio.Application.Exceptions;

/// <summary>
/// Thrown when an email could not be sent: the provider is not configured or did not accept the message.
/// </summary>
/// <param name="message">What went wrong, without secrets.</param>
/// <param name="innerException">The underlying failure, if any.</param>
public sealed class EmailDeliveryException(string message, Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Gets the stable error code.
    /// </summary>
    public string Code => ApplicationErrorCodes.EmailDeliveryFailed;
}
