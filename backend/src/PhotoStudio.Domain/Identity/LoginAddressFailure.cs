namespace PhotoStudio.Domain.Identity;

/// <summary>
/// What is known about the failed logins of one account from one network address: how many in a row, and until when that address
/// is refused for that account. The address is never stored as such, only as a keyed hash. Keeping the count per account <b>and</b>
/// address is what stops a stranger who knows a username from locking its owner out: the stranger exhausts the attempts of their
/// own address, and the owner signing in from another one is not affected.
/// </summary>
/// <param name="UserId">Account the failures are against.</param>
/// <param name="AddressKey">Keyed hash of the address the attempts came from.</param>
/// <param name="Attempts">Consecutive failed logins from the address since its last success or block.</param>
/// <param name="BlockedUntil">Instant until which the address is refused for the account, or <see langword="null"/> when it is not.</param>
public sealed record LoginAddressFailure(Guid UserId, string AddressKey, int Attempts, DateTimeOffset? BlockedUntil)
{
    /// <summary>
    /// Tells whether the address is refused for the account at the given instant.
    /// </summary>
    /// <param name="now">Instant to evaluate.</param>
    /// <returns><see langword="true"/> while the block lasts.</returns>
    public bool IsBlockedAt(DateTimeOffset now) => BlockedUntil is { } until && now < until;
}
