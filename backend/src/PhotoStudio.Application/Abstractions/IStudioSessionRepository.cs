using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Persistence port of the sessions of the web area. A session is found only by the hash of the secret in the cookie.
/// </summary>
public interface IStudioSessionRepository
{
    /// <summary>
    /// Stores a new session.
    /// </summary>
    /// <param name="session">Session to store.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the session is stored.</returns>
    Task AddAsync(StudioSession session, CancellationToken cancellationToken);

    /// <summary>
    /// Finds a session by the hash of its secret.
    /// </summary>
    /// <param name="tokenHash">Hash of the secret in the cookie.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The session, or <see langword="null"/> when there is none.</returns>
    Task<StudioSession?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Records that a session was used.
    /// </summary>
    /// <param name="id">Session identifier.</param>
    /// <param name="lastSeenAt">Instant of the use.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the instant is stored.</returns>
    Task TouchAsync(Guid id, DateTimeOffset lastSeenAt, CancellationToken cancellationToken);

    /// <summary>
    /// Closes a session.
    /// </summary>
    /// <param name="id">Session identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the session is gone.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Closes every session of a user, for example after a change of password.
    /// </summary>
    /// <param name="userId">User whose sessions are closed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the sessions are gone.</returns>
    Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken);
}
