using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Persistence port of the browsers the users chose to remember. A device is found only by the hash of the secret in its cookie.
/// </summary>
public interface IStudioDeviceRepository
{
    /// <summary>
    /// Stores a remembered browser.
    /// </summary>
    /// <param name="device">Device to store.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the device is stored.</returns>
    Task AddAsync(StudioTrustedDevice device, CancellationToken cancellationToken);

    /// <summary>
    /// Finds a device by the hash of its secret.
    /// </summary>
    /// <param name="tokenHash">Hash of the secret in the cookie.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The device, or <see langword="null"/> when there is none.</returns>
    Task<StudioTrustedDevice?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the secret of a device and pushes its expiry forward, only if nobody changed it first. Two requests that arrive with
    /// the same cookie cannot both rotate it: the second one finds the old hash gone.
    /// </summary>
    /// <param name="id">Device identifier.</param>
    /// <param name="oldTokenHash">Hash the device is expected to have.</param>
    /// <param name="newTokenHash">Hash of the new secret.</param>
    /// <param name="lastUsedAt">Instant of the use.</param>
    /// <param name="expiresAt">New expiry.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> when this request rotated it.</returns>
    Task<bool> RotateAsync(Guid id, string oldTokenHash, string newTokenHash, DateTimeOffset lastUsedAt, DateTimeOffset expiresAt, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets a device.
    /// </summary>
    /// <param name="id">Device identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the device is gone.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets every device of a user, for example after a change of password.
    /// </summary>
    /// <param name="userId">User whose devices are forgotten.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the devices are gone.</returns>
    Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken);
}
