using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Domain.UnitTests.Identity;

/// <summary>
/// Tests of <see cref="PasswordResetCode"/>: the lifetime of the code and the limits on how often it can be sent.
/// </summary>
public sealed class PasswordResetCodeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.CreateVersion7();

    /// <summary>
    /// The first code starts a window with one send and lives ten minutes.
    /// </summary>
    [Fact]
    public void Issue_TheFirstCode_StartsAWindowAndLivesTenMinutes()
    {
        var code = PasswordResetCode.Issue(UserId, "hash", null, Now);

        code.ExpiresAt.ShouldBe(Now.AddMinutes(10));
        code.SentAt.ShouldBe(Now);
        code.SendCount.ShouldBe(1);
        code.WindowStartedAt.ShouldBe(Now);
        code.FailedAttempts.ShouldBe(0);
    }

    /// <summary>
    /// A new code inside the window continues its count and starts with no wrong guesses; after the window the count restarts.
    /// </summary>
    [Fact]
    public void Issue_ContinuesTheWindowOnlyWhileItIsOpen()
    {
        var first = PasswordResetCode.Issue(UserId, "one", null, Now) with { FailedAttempts = 3 };

        var inside = PasswordResetCode.Issue(UserId, "two", first, Now.AddMinutes(5));
        var after = PasswordResetCode.Issue(UserId, "three", first, Now.AddMinutes(61));

        inside.SendCount.ShouldBe(2);
        inside.WindowStartedAt.ShouldBe(Now);
        inside.FailedAttempts.ShouldBe(0);
        after.SendCount.ShouldBe(1);
        after.WindowStartedAt.ShouldBe(Now.AddMinutes(61));
    }

    /// <summary>
    /// Another code cannot be sent inside the cooldown, and can right after it.
    /// </summary>
    [Fact]
    public void IsSendBlockedAt_BlocksInsideTheCooldownOnly()
    {
        var code = PasswordResetCode.Issue(UserId, "hash", null, Now);

        code.IsSendBlockedAt(Now.AddSeconds(59)).ShouldBeTrue();
        code.IsSendBlockedAt(Now.AddSeconds(60)).ShouldBeFalse();
    }

    /// <summary>
    /// After five codes in the hour no other is sent until the window ends.
    /// </summary>
    [Fact]
    public void IsSendBlockedAt_BlocksAfterTheHourlyLimitUntilTheWindowEnds()
    {
        var code = new PasswordResetCode(UserId, "hash", Now.AddMinutes(-40), Now.AddMinutes(-50), 0, PasswordResetCode.MaxSendsPerWindow, Now.AddMinutes(-55));

        code.IsSendBlockedAt(Now).ShouldBeTrue();
        code.IsSendBlockedAt(Now.AddMinutes(5)).ShouldBeFalse();
    }

    /// <summary>
    /// A code expires at its expiry instant, not before.
    /// </summary>
    [Fact]
    public void IsExpiredAt_IsTrueFromTheExpiryInstant()
    {
        var code = PasswordResetCode.Issue(UserId, "hash", null, Now);

        code.IsExpiredAt(Now.AddMinutes(10).AddTicks(-1)).ShouldBeFalse();
        code.IsExpiredAt(Now.AddMinutes(10)).ShouldBeTrue();
    }
}
