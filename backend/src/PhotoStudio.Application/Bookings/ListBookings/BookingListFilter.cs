using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Bookings.ListBookings;

/// <summary>
/// Narrows the list of bookings of a photographer. Both criteria are optional and, when both are present, a booking must meet both.
/// </summary>
/// <param name="Status">Only the bookings in this status, or <see langword="null"/> for any status.</param>
/// <param name="NameContains">Only the bookings whose client name contains this text, ignoring case, or <see langword="null"/> for any name.</param>
public sealed record BookingListFilter(BookingStatus? Status = null, string? NameContains = null)
{
    /// <summary>
    /// Longest search text accepted; anything longer is cut, so a request cannot make the database build a huge pattern.
    /// </summary>
    public const int MaxNameLength = 80;

    /// <summary>
    /// A filter that lets every booking through.
    /// </summary>
    public static BookingListFilter None { get; } = new();

    /// <summary>
    /// Indicates whether the filter leaves some bookings out.
    /// </summary>
    public bool IsActive => Status is not null || NameContains is not null;

    /// <summary>
    /// Builds a filter from what the user typed: the text is trimmed, cut to <see cref="MaxNameLength"/> and an empty text means no name criterion.
    /// </summary>
    /// <param name="status">Status wanted, or <see langword="null"/>.</param>
    /// <param name="name">Text typed in the search box, or <see langword="null"/>.</param>
    /// <returns>The filter.</returns>
    public static BookingListFilter From(BookingStatus? status, string? name)
    {
        var text = name?.Trim();
        if (text is { Length: > MaxNameLength })
        {
            text = text[..MaxNameLength].TrimEnd();
        }

        return new BookingListFilter(status, string.IsNullOrEmpty(text) ? null : text);
    }
}
