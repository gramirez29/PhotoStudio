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
}
