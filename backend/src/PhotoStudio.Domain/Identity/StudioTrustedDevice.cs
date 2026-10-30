namespace PhotoStudio.Domain.Identity;

/// <summary>
/// A browser the user chose to remember after the second factor, so it is not asked again for the code for a while. It never
/// replaces the password: it only skips the code. The secret in its cookie changes at every use, so a copied cookie works once
/// at most; only its hash is kept.
/// </summary>
/// <param name="Id">Device identifier.</param>
/// <param name="UserId">User the device belongs to; a device is never accepted for another user.</param>
/// <param name="TokenHash">SHA-256 of the secret in the cookie, never the secret.</param>
/// <param name="CreatedAt">Instant the browser was remembered (UTC).</param>
/// <param name="LastUsedAt">Instant it last signed in (UTC).</param>
/// <param name="ExpiresAt">Instant it is forgotten (UTC); every use pushes it forward.</param>
public sealed record StudioTrustedDevice(Guid Id, Guid UserId, string TokenHash, DateTimeOffset CreatedAt, DateTimeOffset LastUsedAt, DateTimeOffset ExpiresAt)
{
    /// <summary>How long a browser is remembered after its last sign-in.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// Remembers a browser.
    /// </summary>
    /// <param name="userId">User who passed the second factor.</param>
    /// <param name="tokenHash">Hash of the secret that goes in the cookie.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The device.</returns>
    public static StudioTrustedDevice Remember(Guid userId, string tokenHash, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, tokenHash, now, now, now + Lifetime);

    /// <summary>
    /// Tells whether the device can still skip the code for the user.
    /// </summary>
    /// <param name="userId">User who is signing in.</param>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when the device is this user's and has not been forgotten.</returns>
    public bool IsTrustedFor(Guid userId, DateTimeOffset now) => UserId == userId && now < ExpiresAt;
}
