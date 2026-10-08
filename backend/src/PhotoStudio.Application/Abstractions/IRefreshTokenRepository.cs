using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Persistence port for <see cref="RefreshToken"/>. Implemented in the Infrastructure layer.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Stores a newly issued token.
    /// </summary>
    /// <param name="token">Token to store.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the token is stored.</returns>
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    /// <summary>
    /// Finds a token by the hash of its secret.
    /// </summary>
    /// <param name="tokenHash">Hash of the secret presented by the app.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The token, or <see langword="null"/> when none matches.</returns>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically revokes <paramref name="current"/> and stores <paramref name="replacement"/>. Succeeds for exactly one
    /// caller: if the token was already revoked (a concurrent or replayed use), nothing is stored.
    /// </summary>
    /// <param name="current">Token being used; must still be unrevoked in storage.</param>
    /// <param name="replacement">Token that takes its place.</param>
    /// <param name="now">Current instant, stored as the revocation instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> when the rotation happened; <see langword="false"/> when the token was already revoked.</returns>
    Task<bool> RotateAsync(RefreshToken current, RefreshToken replacement, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes every active token of a login family.
    /// </summary>
    /// <param name="familyId">Family to revoke.</param>
    /// <param name="now">Current instant, stored as the revocation instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of tokens revoked.</returns>
    Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes every active token of a photographer, signing out all their devices.
    /// </summary>
    /// <param name="photographerId">Photographer identifier.</param>
    /// <param name="now">Current instant, stored as the revocation instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of tokens revoked.</returns>
    Task<int> RevokeAllAsync(Guid photographerId, DateTimeOffset now, CancellationToken cancellationToken);
}
