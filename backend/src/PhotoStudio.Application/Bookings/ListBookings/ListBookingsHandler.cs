using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Pagination;

namespace PhotoStudio.Application.Bookings.ListBookings;

/// <summary>
/// Lists the photographer's bookings, the most recently created first, a page at a time.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class ListBookingsHandler(
    IBookingRepository repository,
    TimeProvider timeProvider) : IQueryHandler<ListBookingsQuery, BookingListResponse>
{
    /// <summary>
    /// Days of history kept in the list: sessions that ended earlier are left out, because they usually need no action.
    /// </summary>
    public const int HistoryDays = 30;

    /// <summary>
    /// Lists one page of bookings.
    /// </summary>
    /// <param name="query">Query with the photographer identifier and the position of the page.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The page, and where the next one starts when there is one.</returns>
    public async Task<BookingListResponse> HandleAsync(ListBookingsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, 1, PageSizes.Max);
        var endingAfter = timeProvider.GetUtcNow().AddDays(-HistoryDays);
        // One more than the page: if it comes, there is a next page, and it is not part of this one.
        var bookings = await repository.ListByPhotographerAsync(query.PhotographerId, endingAfter, query.After, limit + 1, cancellationToken);

        var page = bookings.Take(limit).ToList();
        var next = bookings.Count > limit ? new PageCursor(page[^1].CreatedAt, page[^1].Id).Encode() : null;

        return new BookingListResponse([.. page.Select(booking => booking.ToSummary())], next);
    }
}
