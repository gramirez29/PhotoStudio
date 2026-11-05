namespace PhotoStudio.Domain.Identity;

/// <summary>
/// The code a user types to choose a new password after forgetting the old one, and the counters that limit how it is used. It
/// follows the rules of the code that verifies an email (ten minutes, one use, five wrong guesses, a minute between codes, five an
/// hour) but lives in its own record, so a code made for one purpose can never be used for the other. Only a keyed hash of the
/// code is kept.
/// </summary>
/// <param name="UserId">User the code is for.</param>
/// <param name="CodeHash">Hash of the code, never the code.</param>
/// <param name="ExpiresAt">Instant the code stops being accepted (UTC).</param>
/// <param name="SentAt">Instant the code was sent (UTC).</param>
/// <param name="FailedAttempts">Wrong guesses counted against this code.</param>
/// <param name="SendCount">Codes sent in the current window, this one included.</param>
/// <param name="WindowStartedAt">Instant the current sending window began (UTC).</param>
public sealed record PasswordResetCode(
    Guid UserId,
    string CodeHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset SentAt,
    int FailedAttempts,
    int SendCount,
    DateTimeOffset WindowStartedAt)
{
    /// <summary>Number of digits of the code.</summary>
    public const int CodeLength = 6;

    /// <summary>How long a code is accepted.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>Wrong guesses a code tolerates before it is spent.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>Minimum wait between two codes of the same user.</summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    /// <summary>Length of the window in which the number of codes is counted.</summary>
    public static readonly TimeSpan SendWindow = TimeSpan.FromHours(1);

    /// <summary>Codes allowed in one window.</summary>
    public const int MaxSendsPerWindow = 5;

    /// <summary>
    /// Text put before the code before hashing it, so the hash of a code made for this purpose is different from the one of the same
    /// digits made for another purpose.
    /// </summary>
    public const string HashPurpose = "password-reset:";

    /// <summary>
    /// Builds the record of a code that is about to be sent, continuing the count of the window when it is still open.
    /// </summary>
    /// <param name="userId">User the code is for.</param>
    /// <param name="codeHash">Hash of the new code.</param>
    /// <param name="previous">Record of the previous code, if there was one.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The record to store.</returns>
    public static PasswordResetCode Issue(Guid userId, string codeHash, PasswordResetCode? previous, DateTimeOffset now)
    {
        var windowOpen = previous is not null && now < previous.WindowStartedAt + SendWindow;
        return new PasswordResetCode(
            userId,
            codeHash,
            now + Lifetime,
            now,
            0,
            windowOpen ? previous!.SendCount + 1 : 1,
            windowOpen ? previous!.WindowStartedAt : now);
    }

    /// <summary>
    /// Tells whether a new code cannot be sent yet, because one went out less than a minute ago or too many went out this hour.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when the user has to wait.</returns>
    public bool IsSendBlockedAt(DateTimeOffset now)
    {
        if (now < SentAt + ResendCooldown)
        {
            return true;
        }

        return SendCount >= MaxSendsPerWindow && now < WindowStartedAt + SendWindow;
    }

    /// <summary>
    /// Tells whether the code can no longer be used because its time ran out.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when the code is expired.</returns>
    public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAt;
}
