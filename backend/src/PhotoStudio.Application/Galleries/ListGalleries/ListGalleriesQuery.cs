using PhotoStudio.Application.Pagination;

namespace PhotoStudio.Application.Galleries.ListGalleries;

/// <summary>
/// Query for one page of the galleries of one photographer.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="After">The last gallery the client already has, or <see langword="null"/> for the first page.</param>
/// <param name="Limit">How many galleries the page has; the handler never returns more.</param>
public sealed record ListGalleriesQuery(Guid PhotographerId, PageCursor? After = null, int Limit = PageSizes.Default);
