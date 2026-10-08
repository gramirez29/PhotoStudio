using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Identity;

/// <summary>
/// Login account of a photographer. Its identifier is the <c>PhotographerId</c> that scopes every booking, so the
/// authenticated account is the tenant of all the data it can reach. The aggregate holds credentials only: the password is
/// stored as a hash produced by the infrastructure, never in clear text.
/// </summary>
public sealed class PhotographerAccount : AggregateRoot<Guid>
{
    /// <summary>
    /// Consecutive failed logins that lock the account.
    /// </summary>
    public const int MaxFailedLoginAttempts = 5;

    /// <summary>
    /// How long a locked account refuses logins.
    /// </summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotographerAccount"/> class. Use <see cref="Create"/> or <see cref="Restore"/>.
    /// </summary>
    private PhotographerAccount(
        Guid id,
        long version,
        string email,
        string passwordHash,
        DateTimeOffset createdAt,
        int failedLoginAttempts,
        DateTimeOffset? lockedUntil)
        : base(id, version)
    {
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
        FailedLoginAttempts = failedLoginAttempts;
        LockedUntil = lockedUntil;
    }

    /// <summary>
    /// Gets the login email, trimmed and in lower case.
    /// </summary>
    public string Email { get; }

    /// <summary>
    /// Gets the password hash, in the format of the password hasher of the infrastructure.
    /// </summary>
    public string PasswordHash { get; }

    /// <summary>
    /// Gets the instant the account was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the consecutive failed logins since the last success.
    /// </summary>
    public int FailedLoginAttempts { get; }

    /// <summary>
    /// Gets the instant until which logins are refused, or <see langword="null"/> when the account is not locked.
    /// </summary>
    public DateTimeOffset? LockedUntil { get; }

    /// <summary>
    /// Creates a new account.
    /// </summary>
    /// <param name="id">Photographer identifier; becomes the tenant of every booking of the photographer.</param>
    /// <param name="email">Login email.</param>
    /// <param name="passwordHash">Hash of the password.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The account.</returns>
    /// <exception cref="DomainException">When a value is missing or the email is not valid.</exception>
    public static PhotographerAccount Create(Guid id, string email, string passwordHash, DateTimeOffset now)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The photographer identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The password hash is required.");
        }

        return new PhotographerAccount(id, 0, NormalizeEmail(email), passwordHash, now, 0, null);
    }

    /// <summary>
    /// Rebuilds an account from persisted data, without validating it again.
    /// </summary>
    /// <param name="id">Photographer identifier.</param>
    /// <param name="version">Stored version.</param>
    /// <param name="email">Stored email.</param>
    /// <param name="passwordHash">Stored password hash.</param>
    /// <param name="createdAt">Creation instant.</param>
    /// <param name="failedLoginAttempts">Consecutive failed logins.</param>
    /// <param name="lockedUntil">Lock end, if locked.</param>
    /// <returns>The restored account.</returns>
    public static PhotographerAccount Restore(
        Guid id,
        long version,
        string email,
        string passwordHash,
        DateTimeOffset createdAt,
        int failedLoginAttempts,
        DateTimeOffset? lockedUntil) =>
        new(id, version, email, passwordHash, createdAt, failedLoginAttempts, lockedUntil);

    /// <summary>
    /// Normalizes an email for storage and lookup: trimmed and lower case.
    /// </summary>
    /// <param name="email">Email typed by the user.</param>
    /// <returns>The normalized email.</returns>
    /// <exception cref="DomainException">When the email is empty or has no valid shape.</exception>
    public static string NormalizeEmail(string email) =>
        TryNormalizeEmail(email) ?? throw new DomainException(DomainErrorCodes.InvalidEmail, "The email is not valid.");

    /// <summary>
    /// Normalizes an email without throwing, for lookups where an invalid value simply matches no account.
    /// </summary>
    /// <param name="email">Email typed by the user.</param>
    /// <returns>The normalized email, or <see langword="null"/> when it is empty or has no valid shape.</returns>
    public static string? TryNormalizeEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > 254)
        {
            return null;
        }

        var at = normalized.IndexOf('@', StringComparison.Ordinal);
        var validShape = at > 0
            && at == normalized.LastIndexOf('@')
            && at < normalized.Length - 1
            && normalized.IndexOf('.', at + 1) > at + 1
            && normalized[^1] != '.'
            && !normalized.Any(char.IsWhiteSpace);

        return validShape ? normalized : null;
    }

    /// <summary>
    /// Indicates whether the account refuses logins at the given instant.
    /// </summary>
    /// <param name="now">Instant to evaluate.</param>
    /// <returns><see langword="true"/> while the lockout lasts.</returns>
    public bool IsLockedAt(DateTimeOffset now) => LockedUntil is { } until && now < until;

    /// <summary>
    /// Indicates whether a number of consecutive failed logins locks the account.
    /// </summary>
    /// <param name="failedAttempts">Consecutive failed logins, including the latest.</param>
    /// <returns><see langword="true"/> when the limit was reached.</returns>
    public static bool ShouldLock(int failedAttempts) => failedAttempts >= MaxFailedLoginAttempts;
}
