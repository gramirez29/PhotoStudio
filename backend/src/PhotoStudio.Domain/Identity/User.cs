using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Identity;

/// <summary>
/// A person who signs in to the app: a photographer. Its identifier is the <c>PhotographerId</c> that scopes every booking,
/// so the authenticated user is the tenant of all the data it can reach. The aggregate holds credentials and contact data
/// only: the password is stored as a hash produced by the infrastructure, never in clear text.
/// </summary>
public sealed class User : AggregateRoot<Guid>
{
    /// <summary>
    /// Consecutive failed logins that lock the user.
    /// </summary>
    public const int MaxFailedLoginAttempts = 5;

    /// <summary>
    /// Shortest accepted username.
    /// </summary>
    public const int MinUsernameLength = 3;

    /// <summary>
    /// Longest accepted username.
    /// </summary>
    public const int MaxUsernameLength = 30;

    /// <summary>
    /// Longest accepted email (the limit of an address in the SMTP standard).
    /// </summary>
    public const int MaxEmailLength = 254;

    /// <summary>
    /// Longest accepted name.
    /// </summary>
    public const int MaxNameLength = 100;

    /// <summary>
    /// Fewest digits of a phone number, without the country-code sign.
    /// </summary>
    public const int MinPhoneDigits = 8;

    /// <summary>
    /// Most digits of a phone number (the E.164 limit), without the country-code sign.
    /// </summary>
    public const int MaxPhoneDigits = 15;

    /// <summary>
    /// Shortest accepted password.
    /// </summary>
    public const int MinPasswordLength = 8;

    /// <summary>
    /// Longest accepted password. Bounded because hashing cost grows with the length, so an unbounded password is a way to
    /// make the server spend time.
    /// </summary>
    public const int MaxPasswordLength = 128;

    /// <summary>
    /// How long a locked user refuses logins.
    /// </summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Initializes a new instance of the <see cref="User"/> class. Use <see cref="Create"/> or <see cref="Restore"/>.
    /// </summary>
    private User(
        Guid id,
        long version,
        string username,
        string email,
        string passwordHash,
        string name,
        string phone,
        DateTimeOffset createdAt,
        int failedLoginAttempts,
        DateTimeOffset? lockedUntil)
        : base(id, version)
    {
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        Name = name;
        Phone = phone;
        CreatedAt = createdAt;
        FailedLoginAttempts = failedLoginAttempts;
        LockedUntil = lockedUntil;
    }

    /// <summary>
    /// Gets the login name, in lower case.
    /// </summary>
    public string Username { get; }

    /// <summary>
    /// Gets the email, trimmed and in lower case. It is not verified yet: nothing sends mail, so it is contact data and the
    /// future channel for recovering a password, not a proof of ownership. It is empty for users created before it existed.
    /// </summary>
    public string Email { get; }

    /// <summary>
    /// Gets the password hash, in the format of the password hasher of the infrastructure.
    /// </summary>
    public string PasswordHash { get; }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the phone number, with spaces and separators removed.
    /// </summary>
    public string Phone { get; }

    /// <summary>
    /// Gets the instant the user was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the consecutive failed logins since the last success.
    /// </summary>
    public int FailedLoginAttempts { get; }

    /// <summary>
    /// Gets the instant until which logins are refused, or <see langword="null"/> when the user is not locked.
    /// </summary>
    public DateTimeOffset? LockedUntil { get; }

    /// <summary>
    /// Creates a new user, validating and normalizing the data.
    /// </summary>
    /// <param name="id">Photographer identifier; becomes the tenant of every booking of the user.</param>
    /// <param name="username">Login name.</param>
    /// <param name="email">Email address.</param>
    /// <param name="passwordHash">Hash of the password.</param>
    /// <param name="name">Display name.</param>
    /// <param name="phone">Phone number.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The user.</returns>
    /// <exception cref="DomainException">When the identifier or the hash is missing, or the username, email, name or phone is not valid.</exception>
    public static User Create(Guid id, string username, string email, string passwordHash, string name, string phone, DateTimeOffset now)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The user identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The password hash is required.");
        }

        return new User(id, 0, NormalizeUsername(username), NormalizeEmail(email), passwordHash, NormalizeName(name), NormalizePhone(phone), now, 0, null);
    }

    /// <summary>
    /// Rebuilds a user from persisted data, without validating it again.
    /// </summary>
    /// <param name="id">Photographer identifier.</param>
    /// <param name="version">Stored version.</param>
    /// <param name="username">Stored username.</param>
    /// <param name="email">Stored email; empty for users created before the email existed.</param>
    /// <param name="passwordHash">Stored password hash.</param>
    /// <param name="name">Stored name.</param>
    /// <param name="phone">Stored phone.</param>
    /// <param name="createdAt">Creation instant.</param>
    /// <param name="failedLoginAttempts">Consecutive failed logins.</param>
    /// <param name="lockedUntil">Lock end, if locked.</param>
    /// <returns>The restored user.</returns>
    public static User Restore(
        Guid id,
        long version,
        string username,
        string email,
        string passwordHash,
        string name,
        string phone,
        DateTimeOffset createdAt,
        int failedLoginAttempts,
        DateTimeOffset? lockedUntil) =>
        new(id, version, username, email, passwordHash, name, phone, createdAt, failedLoginAttempts, lockedUntil);

    /// <summary>
    /// Checks that a password meets the length rules. Only the length is checked: a longer passphrase is better than a
    /// composition rule, and the length limit also bounds the hashing cost.
    /// </summary>
    /// <param name="password">Password in clear text.</param>
    /// <exception cref="DomainException">When the password is shorter than <see cref="MinPasswordLength"/> or longer than <see cref="MaxPasswordLength"/>.</exception>
    public static void ValidatePassword(string? password)
    {
        if (password is null || password.Length < MinPasswordLength)
        {
            throw new DomainException(DomainErrorCodes.WeakPassword, $"The password must have at least {MinPasswordLength} characters.");
        }

        if (password.Length > MaxPasswordLength)
        {
            throw new DomainException(DomainErrorCodes.WeakPassword, $"The password cannot have more than {MaxPasswordLength} characters.");
        }
    }

    /// <summary>
    /// Normalizes a username for storage and lookup: trimmed and in lower case. It must have 3 to 30 characters, made of
    /// letters, digits, dots, hyphens and underscores, and start and end with a letter or a digit.
    /// </summary>
    /// <param name="username">Username typed by the user.</param>
    /// <returns>The normalized username.</returns>
    /// <exception cref="DomainException">When the username has no valid shape.</exception>
    public static string NormalizeUsername(string username) =>
        TryNormalizeUsername(username) ?? throw new DomainException(
            DomainErrorCodes.InvalidUsername,
            $"The username must have {MinUsernameLength} to {MaxUsernameLength} letters, digits, dots, hyphens or underscores, and start and end with a letter or a digit.");

    /// <summary>
    /// Normalizes a username without throwing, for lookups where an invalid value simply matches no user.
    /// </summary>
    /// <param name="username">Username typed by the user.</param>
    /// <returns>The normalized username, or <see langword="null"/> when it has no valid shape.</returns>
    public static string? TryNormalizeUsername(string? username)
    {
        var normalized = username?.Trim().ToLowerInvariant();
        if (normalized is null || normalized.Length < MinUsernameLength || normalized.Length > MaxUsernameLength)
        {
            return null;
        }

        var validCharacters = normalized.All(character => IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
        return validCharacters && IsAsciiLetterOrDigit(normalized[0]) && IsAsciiLetterOrDigit(normalized[^1]) ? normalized : null;
    }

    /// <summary>
    /// Normalizes an email address: trimmed and in lower case, with one at sign, a name before it and a domain with a dot after
    /// it, no spaces, and at most <see cref="MaxEmailLength"/> characters. It checks the shape only, not that the mailbox exists.
    /// </summary>
    /// <param name="email">Email typed by the user.</param>
    /// <returns>The normalized email.</returns>
    /// <exception cref="DomainException">When the email has no valid shape.</exception>
    public static string NormalizeEmail(string email) =>
        TryNormalizeEmail(email) ?? throw new DomainException(DomainErrorCodes.InvalidEmail, "The email is not valid.");

    /// <summary>
    /// Normalizes an email without throwing.
    /// </summary>
    /// <param name="email">Email typed by the user.</param>
    /// <returns>The normalized email, or <see langword="null"/> when it has no valid shape.</returns>
    public static string? TryNormalizeEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > MaxEmailLength)
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
    /// Normalizes a display name: trimmed, and required.
    /// </summary>
    /// <param name="name">Name typed by the user.</param>
    /// <returns>The trimmed name.</returns>
    /// <exception cref="DomainException">When the name is empty or longer than <see cref="MaxNameLength"/>.</exception>
    public static string NormalizeName(string name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > MaxNameLength)
        {
            throw new DomainException(DomainErrorCodes.InvalidName, $"The name is required and cannot have more than {MaxNameLength} characters.");
        }

        return normalized;
    }

    /// <summary>
    /// Normalizes a phone number: spaces, hyphens, dots and parentheses are removed, and an optional leading <c>+</c> is kept.
    /// What remains must be 8 to 15 digits.
    /// </summary>
    /// <param name="phone">Phone typed by the user.</param>
    /// <returns>The phone, for example <c>+50670189220</c> or <c>70189220</c>.</returns>
    /// <exception cref="DomainException">When the phone has no valid shape.</exception>
    public static string NormalizePhone(string phone)
    {
        var cleaned = new string((phone ?? string.Empty).Where(character => !char.IsWhiteSpace(character) && character is not ('-' or '.' or '(' or ')')).ToArray());
        var digits = cleaned.StartsWith('+') ? cleaned[1..] : cleaned;

        if (digits.Length < MinPhoneDigits || digits.Length > MaxPhoneDigits || !digits.All(char.IsAsciiDigit))
        {
            throw new DomainException(DomainErrorCodes.InvalidPhone, $"The phone must have {MinPhoneDigits} to {MaxPhoneDigits} digits.");
        }

        return cleaned;
    }

    /// <summary>
    /// Indicates whether the user refuses logins at the given instant.
    /// </summary>
    /// <param name="now">Instant to evaluate.</param>
    /// <returns><see langword="true"/> while the lockout lasts.</returns>
    public bool IsLockedAt(DateTimeOffset now) => LockedUntil is { } until && now < until;

    /// <summary>
    /// Indicates whether a number of consecutive failed logins locks the user.
    /// </summary>
    /// <param name="failedAttempts">Consecutive failed logins, including the latest.</param>
    /// <returns><see langword="true"/> when the limit was reached.</returns>
    public static bool ShouldLock(int failedAttempts) => failedAttempts >= MaxFailedLoginAttempts;

    /// <summary>
    /// Indicates whether a character is an ASCII letter or digit (lower case once normalized).
    /// </summary>
    /// <param name="character">Character to evaluate.</param>
    /// <returns><see langword="true"/> for a-z and 0-9.</returns>
    private static bool IsAsciiLetterOrDigit(char character) => character is (>= 'a' and <= 'z') or (>= '0' and <= '9');
}
