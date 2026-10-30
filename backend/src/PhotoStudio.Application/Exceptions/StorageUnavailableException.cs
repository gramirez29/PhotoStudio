namespace PhotoStudio.Application.Exceptions;

/// <summary>
/// Thrown when the object storage cannot be used: it is not configured or it did not answer.
/// </summary>
/// <param name="message">What went wrong, without secrets, keys or signed addresses.</param>
/// <param name="innerException">The underlying failure, if any.</param>
public sealed class StorageUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Gets the stable error code.
    /// </summary>
    public string Code => ApplicationErrorCodes.StorageUnavailable;
}
