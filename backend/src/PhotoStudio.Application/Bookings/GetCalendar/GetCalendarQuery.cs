using PhotoStudio.Application.Bookings.Responses;

namespace PhotoStudio.Application.Bookings.GetCalendar;

/// <summary>
/// Limits of the calendar query. They keep one request small however a client asks, and are the same for every photographer.
/// </summary>
public static class CalendarLimits
{
    /// <summary>
    /// Longest range one request can cover, in days: two months of weeks and the days around them, which is what the month view of the app shows.
    /// </summary>
    public const int MaxRangeDays = 62;

    /// <summary>
    /// Most bookings one answer carries. More than this in a range is not a calendar a person can read; the answer says it was cut.
    /// </summary>
    public const int MaxItems = 500;
}

/// <summary>
/// Query for the bookings of one photographer that touch a range of time, for the calendar of the app.
/// </summary>
/// <param name="PhotographerId">Photographer (tenant) identifier.</param>
/// <param name="From">Start of the range (UTC).</param>
/// <param name="To">End of the range (UTC), exclusive.</param>
/// <param name="IncludeEnded">When <see langword="true"/>, also the bookings that are over (completed, client absent, cancelled and expired); otherwise only the tentative and the confirmed ones.</param>
public sealed record GetCalendarQuery(Guid PhotographerId, DateTimeOffset From, DateTimeOffset To, bool IncludeEnded = false);

/// <summary>
/// The bookings of a range, ordered by the start of the session.
/// </summary>
/// <param name="Items">The bookings whose session overlaps the range, at most <see cref="CalendarLimits.MaxItems"/>.</param>
/// <param name="Truncated"><see langword="true"/> when there were more than <see cref="CalendarLimits.MaxItems"/> and the later ones were left out.</param>
public sealed record CalendarResponse(IReadOnlyList<BookingSummaryResponse> Items, bool Truncated);
