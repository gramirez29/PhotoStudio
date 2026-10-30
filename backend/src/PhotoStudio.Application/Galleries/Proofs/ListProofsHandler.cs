using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Application.Galleries.Proofs;

/// <summary>
/// Query for the proofs of a gallery, with the addresses to see them.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery whose proofs are listed.</param>
public sealed record ListProofsQuery(Guid PhotographerId, Guid GalleryId);

/// <summary>
/// Lists the proofs of a gallery of the photographer, each with a signed address that works for a few minutes. The addresses are made
/// on every request and are never stored: the page asks again when it needs fresh ones.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="storage">Object storage.</param>
public sealed class ListProofsHandler(IGalleryRepository galleries, IObjectStorage storage) : IQueryHandler<ListProofsQuery, ProofListResponse>
{
    /// <summary>
    /// How long the address of a proof works.
    /// </summary>
    public static readonly TimeSpan UrlLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Lists the proofs.
    /// </summary>
    /// <param name="query">Photographer and gallery.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The proofs.</returns>
    /// <exception cref="Exceptions.NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    public async Task<ProofListResponse> HandleAsync(ListProofsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gallery = await galleries.GetOwnedAsync(query.GalleryId, query.PhotographerId, cancellationToken);
        var items = new List<ProofItemResponse>(gallery.Photos.Count);
        foreach (var photo in gallery.Photos)
        {
            var url = await storage.CreateDownloadUrlAsync(GalleryKeys.Proof(gallery.Id, photo.Id), UrlLifetime, null, cancellationToken);
            items.Add(new ProofItemResponse(photo.Id, photo.OriginalName, photo.ProofBytes, url.Url, url.ExpiresAt));
        }

        return new ProofListResponse(items);
    }
}
