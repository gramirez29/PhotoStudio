using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;

namespace PhotoStudio.Application.Bookings.ListBookings;

/// <summary>
/// Lists the photographer's bookings ordered by session start, so the app can show them without typing identifiers.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class ListBookingsHandler(
    IBookingRepository repository,
    TimeProvider timeProvider) : IQueryHandler<ListBookingsQuery, IReadOnlyList<BookingSummaryResponse>>
{
    /// <summary>
    /// Days of history kept in the list: sessions that ended earlier are left out, because they usually need no action.
    /// </summary>
    public const int HistoryDays = 30;

    /// <summary>
    /// Maximum number of bookings returned, to bound the response until pagination exists.
    /// </summary>
    public const int MaxResults = 200;

    /// <summary>
    /// Lists the bookings.
    /// </summary>
    /// <param name="query">Query with the photographer identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The booking summaries, earliest session first.</returns>
    public async Task<IReadOnlyList<BookingSummaryResponse>> HandleAsync(ListBookingsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var endingAfter = timeProvider.GetUtcNow().AddDays(-HistoryDays);
        var bookings = await repository.ListByPhotographerAsync(query.PhotographerId, endingAfter, MaxResults, cancellationToken);

        return [.. bookings.Select(booking => booking.ToSummary())];
    }
}
