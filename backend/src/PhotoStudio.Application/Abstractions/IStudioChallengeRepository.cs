using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Persistence port of the second-factor challenges of the web sign-in. There is at most one per user.
/// </summary>
public interface IStudioChallengeRepository
{
    /// <summary>
    /// Finds the challenge of a user.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The challenge, or <see langword="null"/> when the user has none.</returns>
    Task<StudioLoginChallenge?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Finds a challenge by the hash of the secret in the cookie of the browser.
    /// </summary>
    /// <param name="tokenHash">Hash of the secret.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The challenge, or <see langword="null"/> when there is none.</returns>
    Task<StudioLoginChallenge?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a challenge, replacing the one the user had.
    /// </summary>
    /// <param name="challenge">Challenge to store.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the challenge is stored.</returns>
    Task SaveAsync(StudioLoginChallenge challenge, CancellationToken cancellationToken);

    /// <summary>
    /// Counts one guess against the challenge, atomically and before the code is compared, so guesses made in parallel cannot
    /// get more than the allowed number.
    /// </summary>
    /// <param name="tokenHash">Hash of the secret of the challenge.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The challenge with the guess counted, or <see langword="null"/> when there is none.</returns>
    Task<StudioLoginChallenge?> RegisterAttemptAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the challenge of a user.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> when this call deleted it; <see langword="false"/> when it was already gone (another request
    /// spent it first).</returns>
    Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken);
}
