using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Persistence port for <see cref="User"/>. Implemented in the Infrastructure layer.
/// </summary>
/// <remarks>
/// The failed-login counter and the lock are changed with dedicated atomic operations instead of load-modify-save: with
/// optimistic concurrency, an attacker sending many guesses in parallel would make most of the writes conflict and the
/// counter would barely move, defeating the lockout.
/// </remarks>
public interface IUserRepository
{
    /// <summary>
    /// Loads a user by its normalized username.
    /// </summary>
    /// <param name="normalizedUsername">Username already trimmed and in lower case.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The user, or <see langword="null"/> when none uses that username.</returns>
    Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a user by its identifier.
    /// </summary>
    /// <param name="id">Photographer identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The user, or <see langword="null"/> when it does not exist.</returns>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new user. The unique indexes on the username and on the email are the authority: checking first and
    /// inserting after would let two simultaneous registrations take the same name or address.
    /// </summary>
    /// <param name="user">User to store.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the user is stored.</returns>
    /// <exception cref="Exceptions.ConflictException">When the username or the email is already used.</exception>
    Task AddAsync(User user, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically counts one more failed login.
    /// </summary>
    /// <param name="id">Photographer identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of consecutive failed logins after counting this one.</returns>
    Task<int> RegisterFailedLoginAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Locks the user until the given instant and restarts the failed-login counter.
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
}
