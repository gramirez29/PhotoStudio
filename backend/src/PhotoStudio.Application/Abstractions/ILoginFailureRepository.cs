using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Port that stores the failed logins of each account per network address. Counting is atomic, for the same reason as the
/// account-wide counter: guesses sent in parallel must not slip past the limit.
/// </summary>
public interface ILoginFailureRepository
{
    /// <summary>
    /// Reads what is known about the failures of an account from an address.
    /// </summary>
    /// <param name="userId">Account.</param>
    /// <param name="addressKey">Keyed hash of the address.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The record, or <see langword="null"/> when there is none.</returns>
    Task<LoginAddressFailure?> GetAsync(Guid userId, string addressKey, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically counts one more failed login of an account from an address, creating the record if it does not exist.
    /// </summary>
    /// <param name="userId">Account.</param>
    /// <param name="addressKey">Keyed hash of the address.</param>
    /// <param name="now">Current instant, to know when the record can be forgotten.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of consecutive failures from the address after counting this one.</returns>
    Task<int> RegisterFailureAsync(Guid userId, string addressKey, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Refuses an address for an account until the given instant and restarts its counter.
    /// </summary>
    /// <param name="userId">Account.</param>
    /// <param name="addressKey">Keyed hash of the address.</param>
    /// <param name="until">Instant the block ends.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the block is stored.</returns>
    Task BlockAsync(Guid userId, string addressKey, DateTimeOffset until, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets the failures of an account from an address, after a successful login from it.
    /// </summary>
    /// <param name="userId">Account.</param>
    /// <param name="addressKey">Keyed hash of the address.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the record is gone.</returns>
    Task DeleteAsync(Guid userId, string addressKey, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets the failures of an account from every address, after its password was changed or recovered.
    /// </summary>
    /// <param name="userId">Account.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the records are gone.</returns>
    Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken);
}
