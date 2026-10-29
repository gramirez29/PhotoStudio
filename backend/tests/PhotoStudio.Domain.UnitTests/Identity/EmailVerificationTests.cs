using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Domain.UnitTests.Identity;

/// <summary>
/// Tests of <see cref="EmailVerification"/> and of the verification fields of <see cref="User"/>.
/// </summary>
public sealed class EmailVerificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.CreateVersion7();

    /// <summary>
    /// The first code of a user opens a window with one send, expires in ten minutes and has no wrong guesses.
    /// </summary>
    [Fact]
    public void Issue_WithoutAPreviousCode_StartsAWindow()
    {
        var record = EmailVerification.Issue(UserId, "hash", null, Now);

        record.UserId.ShouldBe(UserId);
        record.ExpiresAt.ShouldBe(Now + EmailVerification.Lifetime);
        record.FailedAttempts.ShouldBe(0);
        record.SendCount.ShouldBe(1);
        record.WindowStartedAt.ShouldBe(Now);
    }

    /// <summary>
    /// A new code inside the window counts one more send and keeps the window, but starts again with no wrong guesses.
    /// </summary>
    [Fact]
    public void Issue_InsideTheWindow_CountsAnotherSendAndResetsTheGuesses()
    {
        var first = EmailVerification.Issue(UserId, "hash-1", null, Now) with { FailedAttempts = 3 };

        var second = EmailVerification.Issue(UserId, "hash-2", first, Now.AddMinutes(5));

        second.SendCount.ShouldBe(2);
        second.WindowStartedAt.ShouldBe(Now);
        second.FailedAttempts.ShouldBe(0);
        second.CodeHash.ShouldBe("hash-2");
    }

    /// <summary>
    /// A new code after the window has passed starts a new window.
    /// </summary>
    [Fact]
    public void Issue_AfterTheWindow_StartsANewWindow()
    {
        var first = EmailVerification.Issue(UserId, "hash-1", null, Now) with { SendCount = 5 };

        var later = EmailVerification.Issue(UserId, "hash-2", first, Now + EmailVerification.SendWindow + TimeSpan.FromMinutes(1));

        later.SendCount.ShouldBe(1);
        later.WindowStartedAt.ShouldBe(Now + EmailVerification.SendWindow + TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Another code cannot be sent before a minute has passed.
    /// </summary>
    [Fact]
    public void RetryAfterAt_InsideTheCooldown_ReturnsTheTimeLeft()
    {
        var record = EmailVerification.Issue(UserId, "hash", null, Now);

        record.RetryAfterAt(Now.AddSeconds(20)).ShouldBe(TimeSpan.FromSeconds(40));
        record.RetryAfterAt(Now + EmailVerification.ResendCooldown).ShouldBeNull();
    }

    /// <summary>
    /// After five sends in an hour the user waits until the window ends.
    /// </summary>
    [Fact]
    public void RetryAfterAt_AfterTheHourlyCap_ReturnsTheRestOfTheWindow()
    {
        var record = EmailVerification.Issue(UserId, "hash", null, Now) with { SendCount = EmailVerification.MaxSendsPerWindow };
        var now = Now.AddMinutes(10);

        record.RetryAfterAt(now).ShouldBe(Now + EmailVerification.SendWindow - now);
        record.RetryAfterAt(Now + EmailVerification.SendWindow).ShouldBeNull();
    }

    /// <summary>
    /// A code is expired exactly when its time runs out.
    /// </summary>
    [Fact]
    public void IsExpiredAt_IsTrueFromTheExpiryOn()
    {
        var record = EmailVerification.Issue(UserId, "hash", null, Now);

        record.IsExpiredAt(Now + EmailVerification.Lifetime - TimeSpan.FromSeconds(1)).ShouldBeFalse();
        record.IsExpiredAt(Now + EmailVerification.Lifetime).ShouldBeTrue();
    }

    /// <summary>
    /// A new user starts with an email that is not verified.
    /// </summary>
    [Fact]
    public void User_Create_StartsWithTheEmailNotVerified()
    {
        var user = User.Create(Guid.CreateVersion7(), "ana", "ana@example.com", "hash", "Ana Pérez", "70189220", Now);

        user.IsEmailVerified.ShouldBeFalse();
        user.EmailVerifiedAt.ShouldBeNull();
    }

    /// <summary>
    /// A user restored with the instant is verified, and one restored without it (an older document) is not.
    /// </summary>
    [Fact]
    public void User_Restore_ReadsTheVerificationInstant()
    {
        var verified = User.Restore(Guid.CreateVersion7(), 1, "ana", "ana@example.com", "hash", "Ana", "70189220", Now, 0, null, false, Now.AddHours(1));
        var older = User.Restore(Guid.CreateVersion7(), 1, "luis", "luis@example.com", "hash", "Luis", "70189221", Now, 0, null);

        verified.IsEmailVerified.ShouldBeTrue();
        verified.EmailVerifiedAt.ShouldBe(Now.AddHours(1));
        older.IsEmailVerified.ShouldBeFalse();
    }
}
