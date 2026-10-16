using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Portal;

namespace PhotoStudio.Domain.UnitTests.Portal;

/// <summary>
/// Tests of <see cref="BookingLink"/> and of the sending rules of <see cref="PortalOtp"/>.
/// </summary>
public sealed class PortalRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A link works until it expires, and not after.
    /// </summary>
    [Fact]
    public void BookingLink_IsActiveUntilItExpires()
    {
        var link = BookingLink.Issue(Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", Now, Now.AddDays(10));

        link.IsActiveAt(Now.AddDays(10).AddTicks(-1)).ShouldBeTrue();
        link.IsActiveAt(Now.AddDays(10)).ShouldBeFalse();
    }

    /// <summary>
    /// A revoked link never works again.
    /// </summary>
    [Fact]
    public void BookingLink_WhenRevoked_IsNotActive()
    {
        var link = BookingLink.Restore(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", Now, Now.AddDays(10), Now.AddDays(1));

        link.IsActiveAt(Now.AddDays(2)).ShouldBeFalse();
    }

    /// <summary>
    /// A link needs its booking, owner and hash, and must expire in the future.
    /// </summary>
    [Fact]
    public void BookingLink_RejectsMissingValuesAndPastExpiry()
    {
        Should.Throw<DomainException>(() => BookingLink.Issue(Guid.Empty, Guid.CreateVersion7(), "hash", Now, Now.AddDays(1)));
        Should.Throw<DomainException>(() => BookingLink.Issue(Guid.CreateVersion7(), Guid.CreateVersion7(), " ", Now, Now.AddDays(1)));
        Should.Throw<DomainException>(() => BookingLink.Issue(Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", Now, Now));
    }

    /// <summary>
    /// The first code starts a window with one send.
    /// </summary>
    [Fact]
    public void PortalOtp_FirstCode_StartsAWindow()
    {
        var otp = PortalOtp.Issue(Guid.CreateVersion7(), "h", null, Now);

        otp.SendCount.ShouldBe(1);
        otp.WindowStartedAt.ShouldBe(Now);
        otp.ExpiresAt.ShouldBe(Now + PortalOtp.Lifetime);
        otp.FailedAttempts.ShouldBe(0);
        otp.RetryAfterAt(Now).ShouldBe(PortalOtp.ResendCooldown);
    }

    /// <summary>
    /// A code asked for after the window ended starts a new window, so the cap does not last forever.
    /// </summary>
    [Fact]
    public void PortalOtp_AfterTheWindow_StartsANewOne()
    {
        var first = PortalOtp.Issue(Guid.CreateVersion7(), "h", null, Now);
        var capped = first with { SendCount = PortalOtp.MaxSendsPerWindow };

        var next = PortalOtp.Issue(first.BookingId, "h2", capped, Now + PortalOtp.SendWindow);

        next.SendCount.ShouldBe(1);
        next.WindowStartedAt.ShouldBe(Now + PortalOtp.SendWindow);
    }

    /// <summary>
    /// The hourly cap lifts when its window ends, even though the cooldown already passed.
    /// </summary>
    [Fact]
    public void PortalOtp_AtTheCap_WaitsForTheWindowToEnd()
    {
        var capped = PortalOtp.Issue(Guid.CreateVersion7(), "h", null, Now) with { SendCount = PortalOtp.MaxSendsPerWindow };

        capped.RetryAfterAt(Now.AddMinutes(30)).ShouldBe(TimeSpan.FromMinutes(30));
        capped.RetryAfterAt(Now + PortalOtp.SendWindow).ShouldBeNull();
    }

    /// <summary>
    /// A code that is not capped can be requested again once the cooldown passes.
    /// </summary>
    [Fact]
    public void PortalOtp_BelowTheCap_CanBeRequestedAfterTheCooldown()
    {
        var otp = PortalOtp.Issue(Guid.CreateVersion7(), "h", null, Now);

        otp.RetryAfterAt(Now + PortalOtp.ResendCooldown).ShouldBeNull();
    }

    /// <summary>
    /// A code expires exactly at its lifetime.
    /// </summary>
    [Fact]
    public void PortalOtp_ExpiresAtItsLifetime()
    {
        var otp = PortalOtp.Issue(Guid.CreateVersion7(), "h", null, Now);

        otp.IsExpiredAt(Now + PortalOtp.Lifetime - TimeSpan.FromTicks(1)).ShouldBeFalse();
        otp.IsExpiredAt(Now + PortalOtp.Lifetime).ShouldBeTrue();
    }
}
