using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.Notifications.Push;

/// <summary>
/// Registers the phone of a photographer. It is idempotent: the app registers on every sign-in, and the same token only
/// refreshes its last-seen instant.
/// </summary>
/// <param name="devices">Push device repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class RegisterPushDeviceHandler(IPushDeviceRepository devices, TimeProvider timeProvider)
    : ICommandHandler<RegisterPushDeviceCommand, bool>
{
    /// <summary>
    /// Registers the device.
    /// </summary>
    /// <param name="command">Device to register.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Always <see langword="true"/>: there is nothing to report back.</returns>
    /// <exception cref="Domain.Common.DomainException">When the token is not an Expo push token.</exception>
    public async Task<bool> HandleAsync(RegisterPushDeviceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var device = PushDevice.Register(command.Token, command.PhotographerId, command.Platform, timeProvider.GetUtcNow());
        await devices.UpsertAsync(device, cancellationToken);
        return true;
    }
}

/// <summary>
/// Forgets the phone of a photographer. A token that is unknown or belongs to someone else is ignored, so signing out never
/// fails because of the push.
/// </summary>
/// <param name="devices">Push device repository.</param>
public sealed class UnregisterPushDeviceHandler(IPushDeviceRepository devices) : ICommandHandler<UnregisterPushDeviceCommand, bool>
{
    /// <summary>
    /// Removes the device.
    /// </summary>
    /// <param name="command">Device to remove.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    public async Task<bool> HandleAsync(UnregisterPushDeviceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await devices.DeleteAsync(command.PhotographerId, command.Token, cancellationToken);
        return true;
    }
}
