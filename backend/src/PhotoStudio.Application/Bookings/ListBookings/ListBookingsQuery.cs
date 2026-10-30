using PhotoStudio.Application.Pagination;

namespace PhotoStudio.Application.Bookings.ListBookings;

/// <summary>
/// Query for one page of the bookings of one photographer that still matter: upcoming sessions and the recently finished ones.
/// </summary>
/// <param name="PhotographerId">Photographer (tenant) identifier.</param>
/// <param name="After">The last booking the client already has, or <see langword="null"/> for the first page.</param>
/// <param name="Limit">How many bookings the page has; the handler never returns more.</param>
public sealed record ListBookingsQuery(Guid PhotographerId, PageCursor? After = null, int Limit = PageSizes.Default);
