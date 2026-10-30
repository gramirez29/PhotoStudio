namespace PhotoStudio.Domain.Identity;

/// <summary>
/// The second factor of a web sign-in that is waiting for its code: the user already gave the right password, and the code sent to
/// their email must come back from the same browser (the secret of the challenge travels in a cookie, and only its hash is kept).
/// There is one per user. The code follows the rules of the other codes of the product: it lives ten minutes, works once, allows
/// five wrong guesses, and a new one cannot be asked for before a minute has passed nor more than five times an hour.
/// </summary>
/// <param name="UserId">User who is signing in.</param>
/// <param name="TokenHash">SHA-256 of the secret in the cookie of the challenge, never the secret.</param>
/// <param name="CodeHash">Hash of the code, never the code.</param>
/// <param name="ExpiresAt">Instant the code stops being accepted (UTC).</param>
/// <param name="SentAt">Instant the code was sent (UTC).</param>
/// <param name="FailedAttempts">Wrong guesses counted against this code.</param>
/// <param name="SendCount">Codes sent in the current window, this one included.</param>
/// <param name="WindowStartedAt">Instant the current sending window began (UTC).</param>
public sealed record StudioLoginChallenge(
    Guid UserId,
    string TokenHash,
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
    /// Builds the record of a code that is about to be sent, continuing the count of the window when it is still open.
    /// </summary>
    /// <param name="userId">User who is signing in.</param>
    /// <param name="tokenHash">Hash of the secret of the challenge.</param>
    /// <param name="codeHash">Hash of the new code.</param>
    /// <param name="previous">Record of the previous code of this user, if there was one.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The record to store.</returns>
    public static StudioLoginChallenge Issue(Guid userId, string tokenHash, string codeHash, StudioLoginChallenge? previous, DateTimeOffset now)
    {
        var windowOpen = previous is not null && now < previous.WindowStartedAt + SendWindow;
        return new StudioLoginChallenge(
            userId,
            tokenHash,
            codeHash,
            now + Lifetime,
            now,
            0,
            windowOpen ? previous!.SendCount + 1 : 1,
            windowOpen ? previous!.WindowStartedAt : now);
    }

    /// <summary>
    /// Tells how long the user must wait before another code can be sent.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns>The time to wait, or <see langword="null"/> when a new code can be sent now.</returns>
    public TimeSpan? RetryAfterAt(DateTimeOffset now)
    {
        var cooldownEnds = SentAt + ResendCooldown;
        if (now < cooldownEnds)
        {
            return cooldownEnds - now;
        }

        var windowEnds = WindowStartedAt + SendWindow;
        return SendCount >= MaxSendsPerWindow && now < windowEnds ? windowEnds - now : null;
    }

    /// <summary>
    /// Tells whether the code can no longer be used because its time ran out.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when the code is expired.</returns>
    public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAt;
}
