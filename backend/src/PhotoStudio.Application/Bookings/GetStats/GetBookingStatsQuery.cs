using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Bookings.GetStats;

/// <summary>
/// Query for how many bookings a photographer has in each status, for the home screen of the app.
/// </summary>
/// <param name="PhotographerId">Photographer (tenant) identifier.</param>
/// <param name="From">Only the bookings whose session starts at or after this instant, or <see langword="null"/> for no lower limit.</param>
/// <param name="To">Only the bookings whose session starts before this instant (exclusive), or <see langword="null"/> for no upper limit.</param>
public sealed record GetBookingStatsQuery(Guid PhotographerId, DateTimeOffset? From = null, DateTimeOffset? To = null);

/// <summary>
/// How many bookings there are in one status.
/// </summary>
/// <param name="Status">The status.</param>
/// <param name="Count">Number of bookings in it; zero when there are none.</param>
public sealed record StatusCountResponse(BookingStatus Status, int Count);

/// <summary>
/// The bookings of a photographer counted by status.
/// </summary>
/// <param name="Total">All the bookings counted.</param>
/// <param name="ByStatus">One entry for every status, in the order of <see cref="BookingStatus"/>, with zero for the ones without bookings, so the shape of the answer never changes.</param>
public sealed record BookingStatsResponse(int Total, IReadOnlyList<StatusCountResponse> ByStatus);
