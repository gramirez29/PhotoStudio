using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Domain.UnitTests.Identity;

/// <summary>
/// Tests of <see cref="LoginAddressFailure"/>: when an address is refused for an account.
/// </summary>
public sealed class LoginAddressFailureTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// An address that was never blocked is not refused.
    /// </summary>
    [Fact]
    public void IsBlockedAt_WithoutABlock_IsFalse()
    {
        new LoginAddressFailure(Guid.CreateVersion7(), "key", 3, null).IsBlockedAt(Now).ShouldBeFalse();
    }

    /// <summary>
    /// An address is refused until the end of its block, and not from that instant on.
    /// </summary>
    [Fact]
    public void IsBlockedAt_IsTrueOnlyBeforeTheBlockEnds()
    {
        var until = Now.AddMinutes(15);
        var failure = new LoginAddressFailure(Guid.CreateVersion7(), "key", 0, until);

        failure.IsBlockedAt(Now).ShouldBeTrue();
        failure.IsBlockedAt(until.AddTicks(-1)).ShouldBeTrue();
        failure.IsBlockedAt(until).ShouldBeFalse();
    }
}
