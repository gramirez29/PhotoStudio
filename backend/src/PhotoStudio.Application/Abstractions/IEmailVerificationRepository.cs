using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Port that stores the code of the email verification of each user. There is at most one record per user: a new code replaces
/// the previous one.
/// </summary>
public interface IEmailVerificationRepository
{
    /// <summary>
    /// Reads the record of a user.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The record, or <see langword="null"/> when no code was sent or it was spent.</returns>
    Task<EmailVerification?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores the record of a code that is about to be sent, replacing any previous one.
    /// </summary>
    /// <param name="verification">Record to store.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the record is stored.</returns>
    Task SaveAsync(EmailVerification verification, CancellationToken cancellationToken);

    /// <summary>
    /// Counts one guess against the code, atomically and before the guess is compared, so guesses made in parallel cannot
    /// avoid the limit.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The record with the guess counted, or <see langword="null"/> when there is no code.</returns>
    Task<EmailVerification?> RegisterAttemptAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the record of a user: the code was used, expired or spent.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the record is gone.</returns>
    Task DeleteAsync(Guid userId, CancellationToken cancellationToken);
}
