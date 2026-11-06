using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Bookings.GetCalendar;

/// <summary>
/// Lists the bookings of a photographer whose session overlaps a range of time, without pages, for the calendar of the app. A booking
/// that starts before the range and ends inside it, or the other way round, is part of it. By default only the bookings that still
/// hold the schedule (tentative and confirmed) are listed; the caller can ask for the ones that are over too.
/// </summary>
/// <param name="repository">Booking repository.</param>
public sealed class GetCalendarHandler(IBookingRepository repository) : IQueryHandler<GetCalendarQuery, CalendarResponse>
{
    /// <summary>
    /// The statuses of the bookings that hold the schedule.
    /// </summary>
    private static readonly BookingStatus[] ActiveStatuses = [BookingStatus.Tentative, BookingStatus.Confirmed];

    /// <summary>
    /// Lists the bookings of the range.
    /// </summary>
    /// <param name="query">Photographer, range and whether the finished bookings are wanted.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The bookings, ordered by the start of the session, and whether some were left out for being too many.</returns>
    /// <exception cref="ArgumentException">When the range is empty, inverted or longer than <see cref="CalendarLimits.MaxRangeDays"/> days.</exception>
    public async Task<CalendarResponse> HandleAsync(GetCalendarQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.To <= query.From || query.To - query.From > TimeSpan.FromDays(CalendarLimits.MaxRangeDays))
        {
            throw new ArgumentException($"The range must be positive and at most {CalendarLimits.MaxRangeDays} days.", nameof(query));
        }

        // One more than the answer can carry: if it comes, something was left out, and it is not part of the answer.
        var bookings = await repository.ListInRangeAsync(
            query.PhotographerId,
            query.From,
            query.To,
            query.IncludeEnded ? null : ActiveStatuses,
            CalendarLimits.MaxItems + 1,
            cancellationToken);

        return new CalendarResponse([.. bookings.Take(CalendarLimits.MaxItems).Select(booking => booking.ToSummary())], bookings.Count > CalendarLimits.MaxItems);
    }
}
