namespace PhotoStudio.Domain.Identity;

/// <summary>
/// A signed-in browser of the web area: what the cookie of the browser stands for. Only the hash of the secret in the cookie is
/// kept, so a leaked database cannot be used to sign in. It ends after a period without use and, whatever its use, after a fixed
/// time, and it can be closed from the server (signing out, or changing the password).
/// </summary>
/// <param name="Id">Session identifier.</param>
/// <param name="UserId">User the session belongs to.</param>
/// <param name="TokenHash">SHA-256 of the secret in the cookie, never the secret.</param>
/// <param name="CreatedAt">Instant the session began (UTC).</param>
/// <param name="LastSeenAt">Instant the session was last used (UTC).</param>
/// <param name="ExpiresAt">Instant the session ends however much it is used (UTC).</param>
public sealed record StudioSession(Guid Id, Guid UserId, string TokenHash, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt, DateTimeOffset ExpiresAt)
{
    /// <summary>Time without use after which the session ends.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(2);

    /// <summary>Time after which the session ends even if it is in constant use.</summary>
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(12);

    /// <summary>How often the "last seen" instant is written: a use closer than this to the previous one is not stored.</summary>
    public static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Starts a session.
    /// </summary>
    /// <param name="userId">User signing in.</param>
    /// <param name="tokenHash">Hash of the secret that goes in the cookie.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The session.</returns>
    public static StudioSession Start(Guid userId, string tokenHash, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, tokenHash, now, now, now + AbsoluteLifetime);

    /// <summary>
    /// Tells whether the session still works: its time has not run out and it has not been idle for too long.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when the session can be used.</returns>
    public bool IsValidAt(DateTimeOffset now) => now < ExpiresAt && now < LastSeenAt + IdleTimeout;

    /// <summary>
    /// Tells whether using the session now is worth storing a new "last seen" instant.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when enough time passed since the last stored use.</returns>
    public bool ShouldTouchAt(DateTimeOffset now) => now >= LastSeenAt + TouchInterval;
}
