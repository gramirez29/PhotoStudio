using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Domain.Common;
using static PhotoStudio.Domain.UnitTests.Bookings.BookingTestData;

namespace PhotoStudio.Domain.UnitTests.Bookings;

/// <summary>
/// Tests of transition B1 (create).
/// </summary>
public sealed class BookingCreationTests
{
    /// <summary>
    /// A new booking starts tentative, holds the slot for the policy hours and raises <see cref="BookingCreated"/>.
    /// </summary>
    [Fact]
    public void Create_WithValidData_StartsTentativeWithHoldAndEvent()
    {
        var booking = CreateTentative();

        booking.Status.ShouldBe(BookingStatus.Tentative);
        booking.ExpiresAt.ShouldBe(Now.AddHours(BookingPolicy.Default.TentativeHoldHours));
        booking.Version.ShouldBe(0);
        booking.History.Count.ShouldBe(1);
        booking.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<BookingCreated>();
    }

    /// <summary>
    /// When the session starts before the hold would end, the hold ends at the session start.
    /// </summary>
    [Fact]
    public void Create_WithSessionSoonerThanHold_ExpiresAtSessionStart()
    {
        var sessionStart = Now.AddHours(5);

        var booking = CreateTentative(sessionStart: sessionStart);

        booking.ExpiresAt.ShouldBe(sessionStart);
    }

    /// <summary>
    /// A session in the past cannot be booked.
    /// </summary>
    [Fact]
    public void Create_WithSessionInThePast_Throws()
    {
        var exception = Should.Throw<DomainException>(() => CreateTentative(sessionStart: Now.AddHours(-1)));

        exception.Code.ShouldBe(DomainErrorCodes.SessionInPast);
    }

    /// <summary>
    /// The deposit is computed from the copied policy and the balance starts at the full price.
    /// </summary>
    [Fact]
    public void Create_ComputesDepositAndBalanceFromPolicy()
    {
        var booking = CreateTentative();

        booking.DepositRequired.Amount.ShouldBe(PackagePrice / 2);
        booking.Balance.Amount.ShouldBe(PackagePrice);
        booking.IsDepositCovered.ShouldBeFalse();
    }
}
