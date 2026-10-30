using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Pagination;

namespace PhotoStudio.Application.Galleries.ListGalleries;

/// <summary>
/// Lists the photographer's galleries, the most recently created first, a page at a time.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
public sealed class ListGalleriesHandler(IGalleryRepository galleries) : IQueryHandler<ListGalleriesQuery, GalleryListResponse>
{
    /// <summary>
    /// Lists one page of galleries.
    /// </summary>
    /// <param name="query">Query with the photographer and the position of the page.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The page, and where the next one starts when there is one.</returns>
    public async Task<GalleryListResponse> HandleAsync(ListGalleriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, 1, PageSizes.Max);
        // One more than the page: if it comes, there is a next page, and it is not part of this one.
        var found = await galleries.ListByPhotographerAsync(query.PhotographerId, query.After, limit + 1, cancellationToken);

        var page = found.Take(limit).ToList();
        var next = found.Count > limit ? new PageCursor(page[^1].CreatedAt, page[^1].Id).Encode() : null;

        return new GalleryListResponse([.. page.Select(gallery => gallery.ToResponse())], next);
    }
}
