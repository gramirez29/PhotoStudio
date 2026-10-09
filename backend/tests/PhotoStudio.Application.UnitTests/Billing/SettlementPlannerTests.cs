using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Billing;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Billing;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Billing;

/// <summary>
/// Tests of <see cref="SettlementPlanner"/>.
/// </summary>
public sealed class SettlementPlannerTests
{
    private static readonly DateTimeOffset Now = BookingFactory.SessionStart.AddDays(-1);

    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly ISettlementRepository _settlements = Substitute.For<ISettlementRepository>();
    private readonly SettlementPlanner _planner;

    /// <summary>
    /// Creates the fixture.
    /// </summary>
    public SettlementPlannerTests() => _planner = new SettlementPlanner(_bookings, _settlements);

    /// <summary>
    /// Makes the repository return a booking.
    /// </summary>
    /// <param name="booking">Booking to return.</param>
    private void Returns(Booking booking) =>
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

    /// <summary>
    /// Builds a confirmed booking that the photographer cancelled.
    /// </summary>
    /// <returns>The booking.</returns>
    private static Booking CancelledByPhotographer()
    {
        var booking = BookingFactory.Confirmed();
        booking.Cancel(Actor.Photographer, "Sick", Now);
        return booking;
    }

    /// <summary>
    /// A booking that ended with money paid gets a settlement with a snapshot of the client.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Reconcile_ForAnEndedBookingWithPayments_OpensTheSettlement()
    {
        var booking = CancelledByPhotographer();
        Returns(booking);
        _settlements.TryAddAsync(Arg.Any<BookingSettlement>(), Arg.Any<CancellationToken>()).Returns(true);

        await _planner.ReconcileAsync(booking.Id, Now, TestContext.Current.CancellationToken);

        await _settlements.Received(1).TryAddAsync(
            Arg.Is<BookingSettlement>(settlement =>
                settlement.BookingId == booking.Id
                && settlement.PhotographerId == booking.PhotographerId
                && settlement.Reason == SettlementReason.PhotographerCancelled
                && settlement.RefundAmount == Money.Create(50_000m)
                && settlement.Snapshot.ClientName == booking.Client.Name),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A booking that ended with nothing paid gets no settlement.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Reconcile_WithNothingPaid_OpensNothing()
    {
        var booking = BookingFactory.Tentative();
        booking.Cancel(Actor.Photographer, null, Now);
        Returns(booking);

        await _planner.ReconcileAsync(booking.Id, Now, TestContext.Current.CancellationToken);

        await _settlements.DidNotReceiveWithAnyArgs().TryAddAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A missing booking is ignored.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Reconcile_ForAMissingBooking_DoesNothing()
    {
        _bookings.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Booking?)null);

        await _planner.ReconcileAsync(Guid.CreateVersion7(), Now, TestContext.Current.CancellationToken);

        await _settlements.DidNotReceiveWithAnyArgs().TryAddAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Reconciling an existing settlement with the same outcome saves nothing: the event can arrive twice.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Reconcile_WhenTheSettlementAlreadyMatches_SavesNothing()
    {
        var booking = CancelledByPhotographer();
        Returns(booking);
        var existing = BookingSettlement.Open(
            booking.PhotographerId,
            booking.Id,
            new SettlementSnapshot(booking.Client.Name, booking.Client.Phone, booking.PackageName, booking.Slot.Start),
            SettlementCalculator.Calculate(booking)!,
            Now);
        _settlements.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(existing);

        await _planner.ReconcileAsync(booking.Id, Now, TestContext.Current.CancellationToken);

        await _settlements.DidNotReceiveWithAnyArgs().UpdateAsync(default!, TestContext.Current.CancellationToken);
        await _settlements.DidNotReceiveWithAnyArgs().TryAddAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// When the booking is active again (absence reverted), the settlement is undone.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Reconcile_WhenTheBookingIsActiveAgain_VoidsTheSettlement()
    {
        var booking = BookingFactory.Confirmed();
        var markedAt = booking.Slot.Start.AddMinutes(BookingPolicy.Default.ClientAbsentToleranceMinutes);
        booking.MarkClientAbsent(markedAt);
        var existing = BookingSettlement.Open(
            booking.PhotographerId,
            booking.Id,
            new SettlementSnapshot(booking.Client.Name, booking.Client.Phone, booking.PackageName, booking.Slot.Start),
            SettlementCalculator.Calculate(booking)!,
            markedAt);
        booking.RevertClientAbsent("By mistake", markedAt.AddHours(1));
        Returns(booking);
        _settlements.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(existing);

        await _planner.ReconcileAsync(booking.Id, markedAt.AddHours(1), TestContext.Current.CancellationToken);

        existing.RetentionStatus.ShouldBe(RetentionStatus.Reversed);
        await _settlements.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// If another event created the settlement first, the planner reads it and applies the outcome to it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Reconcile_WhenAnotherEventCreatedTheSettlementFirst_RetriesAndAppliesIt()
    {
        var booking = CancelledByPhotographer();
        Returns(booking);
        var created = BookingSettlement.Open(
            booking.PhotographerId,
            booking.Id,
            new SettlementSnapshot(booking.Client.Name, booking.Client.Phone, booking.PackageName, booking.Slot.Start),
            SettlementCalculator.Calculate(booking)!,
            Now);
        _settlements.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns((BookingSettlement?)null, created);
        _settlements.TryAddAsync(Arg.Any<BookingSettlement>(), Arg.Any<CancellationToken>()).Returns(false);

        await _planner.ReconcileAsync(booking.Id, Now, TestContext.Current.CancellationToken);

        await _settlements.Received(1).TryAddAsync(Arg.Any<BookingSettlement>(), Arg.Any<CancellationToken>());
        await _settlements.Received(2).GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// If the settlement keeps changing under it, the planner gives up so the outbox retries the event later.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Reconcile_WhenTheConflictPersists_ThrowsAfterTheAttempts()
    {
        var booking = CancelledByPhotographer();
        Returns(booking);
        _settlements.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns((BookingSettlement?)null);
        _settlements.TryAddAsync(Arg.Any<BookingSettlement>(), Arg.Any<CancellationToken>()).Returns(false);

        await Should.ThrowAsync<ConflictException>(
            () => _planner.ReconcileAsync(booking.Id, Now, TestContext.Current.CancellationToken));

        await _settlements.Received(SettlementPlanner.MaxAttempts).TryAddAsync(Arg.Any<BookingSettlement>(), Arg.Any<CancellationToken>());
    }
}
