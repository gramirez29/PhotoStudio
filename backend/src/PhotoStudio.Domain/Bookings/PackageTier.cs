namespace PhotoStudio.Domain.Bookings;

/// <summary>
/// Package the client booked. The kind of session (family portrait, pre-wedding...) is the package name; this is the size of the offer.
/// </summary>
public enum PackageTier
{
    /// <summary>The entry package.</summary>
    Basic,

    /// <summary>The mid-range package.</summary>
    Standard,

    /// <summary>The complete package.</summary>
    Premium,

    /// <summary>A package agreed with the client for this booking.</summary>
    Custom,

    /// <summary>Any other arrangement that is not one of the packages.</summary>
    Other,
}
