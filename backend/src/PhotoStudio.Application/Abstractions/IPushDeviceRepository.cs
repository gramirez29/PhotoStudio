using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Persistence port for <see cref="PushDevice"/>. Implemented in the Infrastructure layer.
/// </summary>
public interface IPushDeviceRepository
{
    /// <summary>
    /// Stores a device, or moves it to the photographer when its token is already registered: a phone that changes hands
    /// stops notifying the previous owner. Registering the same device again only refreshes its last-seen instant.
    /// </summary>
    /// <param name="device">Device to store.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the device is stored.</returns>
    Task UpsertAsync(PushDevice device, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a device of a photographer, for example when they sign out. A token of another photographer, or an unknown
    /// one, is left alone.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="token">Expo push token to remove.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the device is removed.</returns>
    Task DeleteAsync(Guid photographerId, string token, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the devices of a photographer.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The devices, possibly none.</returns>
    Task<IReadOnlyList<PushDevice>> ListByPhotographerAsync(Guid photographerId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the devices with the given tokens, whoever owns them. Used to forget tokens the push service reported as no
    /// longer valid (the app was uninstalled).
    /// </summary>
    /// <param name="tokens">Tokens to remove.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the devices are removed.</returns>
    Task DeleteTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken cancellationToken);
}
