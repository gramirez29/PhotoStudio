using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Bookings.GetStats;

/// <summary>
/// Counts the bookings of a photographer by status, by the start of the session, for the summary of the home screen.
/// </summary>
/// <param name="repository">Booking repository.</param>
public sealed class GetBookingStatsHandler(IBookingRepository repository) : IQueryHandler<GetBookingStatsQuery, BookingStatsResponse>
{
    /// <summary>
    /// Counts the bookings of the period.
    /// </summary>
    /// <param name="query">Photographer and optional period.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The count of every status and the total.</returns>
    /// <exception cref="ArgumentException">When both limits are given and the period is empty or inverted.</exception>
    public async Task<BookingStatsResponse> HandleAsync(GetBookingStatsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query is { From: { } from, To: { } to } && to <= from)
        {
            throw new ArgumentException("The period must end after it starts.", nameof(query));
        }

        var counts = await repository.CountByStatusAsync(query.PhotographerId, query.From, query.To, cancellationToken);

        var byStatus = Enum.GetValues<BookingStatus>()
            .Select(status => new StatusCountResponse(status, counts.GetValueOrDefault(status)))
            .ToList();
        return new BookingStatsResponse(byStatus.Sum(entry => entry.Count), byStatus);
    }
}
