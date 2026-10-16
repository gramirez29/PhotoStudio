namespace PhotoStudio.Domain.Portal;

/// <summary>
/// The verification code a client is currently allowed to use in the portal, sent to the email of the booking. There is at
/// most one per booking: asking for a new code replaces the previous one. Only the hash of the code is stored. The rules
/// about how often a code can be requested and how many guesses it tolerates live here so every use case applies the same ones.
/// </summary>
/// <param name="BookingId">Booking the code belongs to.</param>
/// <param name="CodeHash">Keyed hash of the code.</param>
/// <param name="ExpiresAt">Instant the code stops being accepted (UTC).</param>
/// <param name="SentAt">Instant the code was sent (UTC).</param>
/// <param name="FailedAttempts">Wrong guesses counted so far.</param>
/// <param name="SendCount">Codes sent in the current window, including this one.</param>
/// <param name="WindowStartedAt">Instant the current sending window started (UTC).</param>
public sealed record PortalOtp(
    Guid BookingId,
    string CodeHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset SentAt,
    int FailedAttempts,
    int SendCount,
    DateTimeOffset WindowStartedAt)
{
    /// <summary>
    /// Digits of a code.
    /// </summary>
    public const int CodeLength = 6;

    /// <summary>
    /// How long a code is accepted.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Wrong guesses a code tolerates before it is spent.
    /// </summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>
    /// Minimum time between two codes sent for the same booking.
    /// </summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Length of the window in which <see cref="MaxSendsPerWindow"/> applies.
    /// </summary>
    public static readonly TimeSpan SendWindow = TimeSpan.FromHours(1);

    /// <summary>
    /// Codes that can be sent for one booking in one window; it caps the emails an abuser can make the system send.
    /// </summary>
    public const int MaxSendsPerWindow = 5;

    /// <summary>
    /// Builds the code record for a new send, continuing the sending window of the previous code when it is still open.
    /// </summary>
    /// <param name="bookingId">Booking the code belongs to.</param>
    /// <param name="codeHash">Keyed hash of the new code.</param>
    /// <param name="previous">Record of the previous code, if there is one.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The record to store.</returns>
    public static PortalOtp Issue(Guid bookingId, string codeHash, PortalOtp? previous, DateTimeOffset now)
    {
        var windowOpen = previous is not null && now < previous.WindowStartedAt + SendWindow;
        return new PortalOtp(
            bookingId,
            codeHash,
            now + Lifetime,
            now,
            0,
            windowOpen ? previous!.SendCount + 1 : 1,
            windowOpen ? previous!.WindowStartedAt : now);
    }

    /// <summary>
    /// Computes how long the client must wait before another code can be sent, taking the cooldown and the hourly cap into account.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns>The time to wait, or <see langword="null"/> when a code can be sent now.</returns>
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
    /// Indicates whether the code is past its expiry at the given instant.
    /// </summary>
    /// <param name="now">Instant to evaluate.</param>
    /// <returns><see langword="true"/> when the code expired.</returns>
    public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAt;
}
