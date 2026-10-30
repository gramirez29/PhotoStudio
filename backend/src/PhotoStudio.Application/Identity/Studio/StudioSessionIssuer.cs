using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Portal;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.Studio;

/// <summary>
/// Starts the sessions of the web area and remembers browsers. The secrets come from the same generator as the refresh tokens
/// (256 random bits) and only their hashes are stored.
/// </summary>
/// <param name="sessions">Session repository.</param>
/// <param name="devices">Device repository.</param>
/// <param name="tokens">Generates and hashes secrets.</param>
public sealed class StudioSessionIssuer(IStudioSessionRepository sessions, IStudioDeviceRepository devices, IRefreshTokenGenerator tokens)
{
    /// <summary>
    /// Starts a session for a user.
    /// </summary>
    /// <param name="user">User who signed in.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The session, with the secret for the cookie.</returns>
    public async Task<StudioIssuedSession> StartSessionAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var secret = tokens.Generate();
        var session = StudioSession.Start(user.Id, secret.Hash, now);
        await sessions.AddAsync(session, cancellationToken);
        return new StudioIssuedSession(secret.Secret, session.ExpiresAt, Summarize(user));
    }

    /// <summary>
    /// Remembers the browser of a user who just passed the second factor.
    /// </summary>
    /// <param name="user">User who passed it.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The device, with the secret for the cookie.</returns>
    public async Task<StudioIssuedDevice> RememberDeviceAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var secret = tokens.Generate();
        var device = StudioTrustedDevice.Remember(user.Id, secret.Hash, now);
        await devices.AddAsync(device, cancellationToken);
        return new StudioIssuedDevice(secret.Secret, device.ExpiresAt);
    }

    /// <summary>
    /// Describes a user for the web area.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The summary, with the email masked.</returns>
    public static StudioUserSummary Summarize(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new StudioUserSummary(user.Id, user.Username, user.Name, PortalLinkExtensions.MaskEmail(user.Email));
    }
}
