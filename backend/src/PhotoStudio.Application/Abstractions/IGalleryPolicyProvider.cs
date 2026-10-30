using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Supplies the gallery policy that applies to a photographer when a gallery is created.
/// </summary>
public interface IGalleryPolicyProvider
{
    /// <summary>
    /// Gets the policy for a photographer.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The policy to copy into the new gallery.</returns>
    Task<GalleryPolicy> GetPolicyAsync(Guid photographerId, CancellationToken cancellationToken);
}
