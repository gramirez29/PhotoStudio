using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Application.Identity.Studio;

/// <summary>
/// Closes the session of a browser on the server, so the cookie stops working even if someone kept a copy of it. It can also forget
/// the browser, so the next sign-in asks for the code again. It never fails: a browser that signs out of something that is already
/// gone is signed out all the same.
/// </summary>
/// <param name="sessions">Session repository.</param>
/// <param name="devices">Device repository.</param>
/// <param name="tokens">Hashes secrets.</param>
public sealed class StudioLogoutHandler(IStudioSessionRepository sessions, IStudioDeviceRepository devices, IRefreshTokenGenerator tokens)
    : ICommandHandler<StudioLogoutCommand, bool>
{
    /// <summary>
    /// Closes the session and, if asked, forgets the browser.
    /// </summary>
    /// <param name="command">The cookies of the browser.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    public async Task<bool> HandleAsync(StudioLogoutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!string.IsNullOrWhiteSpace(command.SessionToken)
            && await sessions.GetByTokenHashAsync(tokens.Hash(command.SessionToken), cancellationToken) is { } session)
        {
            await sessions.DeleteAsync(session.Id, cancellationToken);
        }

        if (command.ForgetDevice
            && !string.IsNullOrWhiteSpace(command.DeviceToken)
            && await devices.GetByTokenHashAsync(tokens.Hash(command.DeviceToken), cancellationToken) is { } device)
        {
            await devices.DeleteAsync(device.Id, cancellationToken);
        }

        return true;
    }
}
