using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Persistence port for <see cref="PhotographerAccount"/>. Implemented in the Infrastructure layer.
/// </summary>
/// <remarks>
/// The failed-login counter and the lock are changed with dedicated atomic operations instead of load-modify-save: with
/// optimistic concurrency, an attacker sending many guesses in parallel would make most of the writes conflict and the
/// counter would barely move, defeating the lockout.
/// </remarks>
public interface IPhotographerAccountRepository
{
    /// <summary>
    /// Loads an account by its normalized email.
    /// </summary>
    /// <param name="normalizedEmail">Email already trimmed and in lower case.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The account, or <see langword="null"/> when none uses that email.</returns>
    Task<PhotographerAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>
    /// Loads an account by its identifier.
    /// </summary>
    /// <param name="id">Photographer identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The account, or <see langword="null"/> when it does not exist.</returns>
    Task<PhotographerAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new account.
    /// </summary>
    /// <param name="account">Account to store.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the account is stored.</returns>
    /// <exception cref="Exceptions.ConflictException">When the email or the identifier is already used.</exception>
    Task AddAsync(PhotographerAccount account, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically counts one more failed login.
    /// </summary>
    /// <param name="id">Photographer identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of consecutive failed logins after counting this one.</returns>
    Task<int> RegisterFailedLoginAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Locks the account until the given instant and restarts the failed-login counter.
    /// </summary>
    /// <param name="id">Photographer identifier.</param>
    /// <param name="until">Instant the lock ends.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the lock is stored.</returns>
    Task LockAsync(Guid id, DateTimeOffset until, CancellationToken cancellationToken);

    /// <summary>
    /// Clears the failed-login counter and the lock after a successful login.
    /// </summary>
    /// <param name="id">Photographer identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the counter is cleared.</returns>
    Task ResetFailedLoginsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the password hash and clears the failed-login counter and the lock.
    /// </summary>
    /// <param name="id">Photographer identifier.</param>
    /// <param name="passwordHash">New password hash.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the hash is stored.</returns>
    Task SetPasswordHashAsync(Guid id, string passwordHash, CancellationToken cancellationToken);
}
