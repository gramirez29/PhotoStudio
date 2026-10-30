using PhotoStudio.Application.Pagination;

namespace PhotoStudio.Application.Notifications.List;

/// <summary>
/// Query for one page of the inbox of a photographer.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="After">The last notification the client already has, or <see langword="null"/> for the first page.</param>
/// <param name="Limit">How many notifications the page has; the handler never returns more.</param>
public sealed record ListNotificationsQuery(Guid PhotographerId, PageCursor? After = null, int Limit = PageSizes.Default);
