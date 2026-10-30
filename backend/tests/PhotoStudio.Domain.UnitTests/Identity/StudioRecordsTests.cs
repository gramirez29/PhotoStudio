using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Domain.UnitTests.Identity;

/// <summary>
/// Tests of the records of the web area: the session, the remembered browser and the challenge of the second factor.
/// </summary>
public sealed class StudioRecordsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A new session lasts at most twelve hours, however much it is used.
    /// </summary>
    [Fact]
    public void Session_Start_EndsAtTheFixedLifetime()
    {
        var session = StudioSession.Start(Guid.CreateVersion7(), "hash", Now);

        session.CreatedAt.ShouldBe(Now);
        session.LastSeenAt.ShouldBe(Now);
        session.ExpiresAt.ShouldBe(Now.AddHours(12));
        session.TokenHash.ShouldBe("hash");
    }

    /// <summary>
    /// A session works while it is used and within its time, and not after two hours without use or after its fixed end.
    /// </summary>
    [Fact]
    public void Session_IsValid_OnlyWhileUsedAndWithinItsTime()
    {
        var session = StudioSession.Start(Guid.CreateVersion7(), "hash", Now);

        session.IsValidAt(Now.AddMinutes(119)).ShouldBeTrue();
        session.IsValidAt(Now.AddHours(2)).ShouldBeFalse();

        var used = session with { LastSeenAt = Now.AddHours(11) };
        used.IsValidAt(Now.AddHours(12).AddMinutes(-1)).ShouldBeTrue();
        used.IsValidAt(Now.AddHours(12)).ShouldBeFalse();
    }

    /// <summary>
    /// The "last seen" instant is written at most once a minute.
    /// </summary>
    [Fact]
    public void Session_ShouldTouch_OnlyAfterAMinute()
    {
        var session = StudioSession.Start(Guid.CreateVersion7(), "hash", Now);

        session.ShouldTouchAt(Now.AddSeconds(59)).ShouldBeFalse();
        session.ShouldTouchAt(Now.AddMinutes(1)).ShouldBeTrue();
    }

    /// <summary>
    /// A remembered browser lasts thirty days and is trusted only for the user who remembered it.
    /// </summary>
    [Fact]
    public void Device_IsTrusted_OnlyForItsUserAndWhileItLasts()
    {
        var user = Guid.CreateVersion7();
        var device = StudioTrustedDevice.Remember(user, "hash", Now);

        device.ExpiresAt.ShouldBe(Now.AddDays(30));
        device.IsTrustedFor(user, Now.AddDays(29)).ShouldBeTrue();
        device.IsTrustedFor(user, Now.AddDays(30)).ShouldBeFalse();
        device.IsTrustedFor(Guid.CreateVersion7(), Now).ShouldBeFalse();
    }

    /// <summary>
    /// A first code starts a sending window and expires in ten minutes.
    /// </summary>
    [Fact]
    public void Challenge_Issue_StartsAWindow()
    {
        var user = Guid.CreateVersion7();

        var challenge = StudioLoginChallenge.Issue(user, "token", "code", null, Now);

        challenge.UserId.ShouldBe(user);
        challenge.ExpiresAt.ShouldBe(Now.AddMinutes(10));
        challenge.SendCount.ShouldBe(1);
        challenge.WindowStartedAt.ShouldBe(Now);
        challenge.FailedAttempts.ShouldBe(0);
        challenge.IsExpiredAt(Now.AddMinutes(10).AddSeconds(-1)).ShouldBeFalse();
        challenge.IsExpiredAt(Now.AddMinutes(10)).ShouldBeTrue();
    }

    /// <summary>
    /// A new code in the same hour continues the count, resets the guesses, and a new hour starts a new window.
    /// </summary>
    [Fact]
    public void Challenge_Issue_ContinuesTheWindowAndStartsAnotherAfterAnHour()
    {
        var user = Guid.CreateVersion7();
        var first = StudioLoginChallenge.Issue(user, "token", "one", null, Now) with { FailedAttempts = 3 };

        var second = StudioLoginChallenge.Issue(user, "token", "two", first, Now.AddMinutes(2));
        var later = StudioLoginChallenge.Issue(user, "token", "three", second, Now.AddHours(1).AddMinutes(1));

        second.SendCount.ShouldBe(2);
        second.WindowStartedAt.ShouldBe(Now);
        second.FailedAttempts.ShouldBe(0);
        later.SendCount.ShouldBe(1);
        later.WindowStartedAt.ShouldBe(Now.AddHours(1).AddMinutes(1));
    }

    /// <summary>
    /// A new code cannot be asked for within a minute, nor more than five times an hour.
    /// </summary>
    [Fact]
    public void Challenge_RetryAfter_LimitsTheCodes()
    {
        var user = Guid.CreateVersion7();
        var challenge = StudioLoginChallenge.Issue(user, "token", "code", null, Now);

        challenge.RetryAfterAt(Now.AddSeconds(30)).ShouldBe(TimeSpan.FromSeconds(30));
        challenge.RetryAfterAt(Now.AddSeconds(60)).ShouldBeNull();

        var fifth = challenge with { SendCount = 5 };
        fifth.RetryAfterAt(Now.AddMinutes(5)).ShouldBe(TimeSpan.FromMinutes(55));
        fifth.RetryAfterAt(Now.AddHours(1)).ShouldBeNull();
    }
}
