using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Application.Identity.Studio;

/// <summary>
/// Takes away everything the web area trusts about a user: their sessions, the browsers they asked to remember and any code that is
/// waiting. It runs when the password changes, so whoever had the old password, or a copied cookie, is out.
/// </summary>
/// <param name="sessions">Session repository.</param>
/// <param name="devices">Device repository.</param>
/// <param name="challenges">Challenge repository.</param>
public sealed class StudioAccessRevoker(IStudioSessionRepository sessions, IStudioDeviceRepository devices, IStudioChallengeRepository challenges)
{
    /// <summary>
    /// Revokes the sessions, remembered browsers and pending code of a user.
    /// </summary>
    /// <param name="userId">User whose access is revoked.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when everything is gone.</returns>
    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        await sessions.DeleteAllForUserAsync(userId, cancellationToken);
        await devices.DeleteAllForUserAsync(userId, cancellationToken);
        await challenges.DeleteAsync(userId, cancellationToken);
    }
}
