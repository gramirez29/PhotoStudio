using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity;

/// <summary>
/// Limits the guesses of a password, for every place that asks for one (the login of the app, the login of the web area and the
/// change of password). The limit has two levels. A network address that fails <see cref="User.MaxFailedLoginAttempts"/> times in a
/// row against an account is refused for that account for <see cref="User.LockoutDuration"/>; the owner signing in from another
/// address is not affected, so knowing a username is no longer enough to lock its owner out. The account itself is locked for every
/// address only after <see cref="User.MaxFailedLoginAttemptsAcrossAddresses"/> failures, which one address cannot reach because it
/// is refused first: it takes several. The address is kept only as a keyed hash.
/// </summary>
/// <param name="users">User repository, which holds the account-wide counter and lock.</param>
/// <param name="failures">Failures per account and address.</param>
/// <param name="codeService">Hashes the address with a secret key.</param>
public sealed class LoginAttemptGuard(IUserRepository users, ILoginFailureRepository failures, IOtpCodeService codeService)
{
    /// <summary>
    /// Text used for a request whose address is not known, so all of them share one record.
    /// </summary>
    private const string UnknownAddress = "unknown";

    /// <summary>
    /// Refuses the attempt before the password is looked at when the account is locked or the address is blocked for it.
    /// </summary>
    /// <param name="user">Account being signed in to.</param>
    /// <param name="address">Network address of the caller, if known.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the attempt may go on.</returns>
    /// <exception cref="AccountLockedException">When the account is locked for every address, or this address is blocked for it.</exception>
    public async Task EnsureAllowedAsync(User user, string? address, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.IsLockedAt(now))
        {
            throw new AccountLockedException(user.LockedUntil!.Value - now);
        }

        var record = await failures.GetAsync(user.Id, KeyOf(user, address), cancellationToken);
        if (record is { BlockedUntil: { } until } && record.IsBlockedAt(now))
        {
            throw new AccountLockedException(until - now);
        }
    }

    /// <summary>
    /// Counts a failed attempt against the address and the account, blocking the address or locking the account when a limit is reached.
    /// </summary>
    /// <param name="user">Account that was tried.</param>
    /// <param name="address">Network address of the caller, if known.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the failure is counted.</returns>
    public async Task RegisterFailureAsync(User user, string? address, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var key = KeyOf(user, address);
        if (User.ShouldLock(await failures.RegisterFailureAsync(user.Id, key, now, cancellationToken)))
        {
            await failures.BlockAsync(user.Id, key, now + User.LockoutDuration, cancellationToken);
        }

        if (User.ShouldLockAcrossAddresses(await users.RegisterFailedLoginAsync(user.Id, cancellationToken)))
        {
            await users.LockAsync(user.Id, now + User.LockoutDuration, cancellationToken);
        }
    }

    /// <summary>
    /// Forgets the failures of the address after a good login from it, and the account-wide count.
    /// </summary>
    /// <param name="user">Account that was signed in to.</param>
    /// <param name="address">Network address of the caller, if known.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the failures are forgotten.</returns>
    public async Task RegisterSuccessAsync(User user, string? address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        await failures.DeleteAsync(user.Id, KeyOf(user, address), cancellationToken);
        if (user.FailedLoginAttempts > 0 || user.LockedUntil is not null)
        {
            await users.ResetFailedLoginsAsync(user.Id, cancellationToken);
        }
    }

    /// <summary>
    /// Forgets the failures of an account from every address, after its password changed or was recovered: whoever was guessing the old
    /// one has nothing left to guess.
    /// </summary>
    /// <param name="userId">Account.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the failures are forgotten.</returns>
    public Task ForgetAllAsync(Guid userId, CancellationToken cancellationToken) =>
        failures.DeleteAllForUserAsync(userId, cancellationToken);

    /// <summary>
    /// Builds the key an address is stored under: a keyed hash bound to the account, so the stored value says nothing about the address.
    /// </summary>
    /// <param name="user">Account.</param>
    /// <param name="address">Network address of the caller, if known.</param>
    /// <returns>The key.</returns>
    private string KeyOf(User user, string? address) =>
        codeService.Hash(user.Id, "login-address:" + (string.IsNullOrWhiteSpace(address) ? UnknownAddress : address.Trim()));
}
