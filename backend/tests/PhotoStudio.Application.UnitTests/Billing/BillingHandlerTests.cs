using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Billing;
using PhotoStudio.Application.Billing.CompleteRefund;
using PhotoStudio.Application.Billing.GetSettlement;
using PhotoStudio.Application.Billing.ListRefunds;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Billing;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Billing;

/// <summary>
/// Tests of the billing handlers: the outbox consumer, reading, listing and completing refunds.
/// </summary>
public sealed class BillingHandlerTests
{
    private static readonly DateTimeOffset Now = BookingFactory.SessionStart.AddDays(-1);

    private readonly ISettlementRepository _settlements = Substitute.For<ISettlementRepository>();

    /// <summary>
    /// Builds a settlement with a pending refund of 50 000 for a photographer.
    /// </summary>
    /// <param name="photographerId">Owner.</param>
    /// <returns>The settlement.</returns>
    private static BookingSettlement Pending(Guid photographerId) => BookingSettlement.Open(
        photographerId,
        Guid.CreateVersion7(),
        new SettlementSnapshot("María", "+50688888888", "Retrato", BookingFactory.SessionStart),
        new SettlementOutcome(SettlementReason.PhotographerCancelled, Money.Create(50_000m), Money.Zero(), Money.Create(50_000m)),
        Now);

    /// <summary>
    /// Every event that can change what a booking owes makes the consumer reconcile the booking's settlement.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Consumer_ReconcilesTheSettlementForEveryEndingEvent()
    {
        var bookings = Substitute.For<IBookingRepository>();
        var booking = BookingFactory.Confirmed();
        booking.Cancel(Actor.Photographer, "Sick", Now);
        bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        _settlements.TryAddAsync(Arg.Any<BookingSettlement>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = new BookingSettlementHandler(new SettlementPlanner(bookings, _settlements), new FixedTimeProvider(Now));
        var token = TestContext.Current.CancellationToken;

        await handler.HandleAsync(new BookingCancelled(booking.Id, Actor.Photographer, true, 24, "Sick", Now), token);
        await handler.HandleAsync(new BookingExpired(booking.Id, Now), token);
        await handler.HandleAsync(new ClientMarkedAbsent(booking.Id, Now), token);
        await handler.HandleAsync(new ClientAbsenceReverted(booking.Id, "By mistake", Now), token);

        await bookings.Received(4).GetByIdAsync(booking.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The settlement of a booking is returned to its owner.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task GetSettlement_ReturnsTheOwnersSettlement()
    {
        var settlement = Pending(Guid.CreateVersion7());
        _settlements.GetByBookingIdAsync(settlement.BookingId, Arg.Any<CancellationToken>()).Returns(settlement);

        var response = await new GetSettlementHandler(_settlements).HandleAsync(
            new GetSettlementQuery(settlement.PhotographerId, settlement.BookingId), TestContext.Current.CancellationToken);

        response.RefundAmount.Amount.ShouldBe(50_000m);
        response.RefundStatus.ShouldBe(RefundStatus.Pending);
        response.ClientName.ShouldBe("María");
    }

    /// <summary>
    /// A missing settlement, or one of another photographer, is not found.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task GetAndComplete_OfAnotherPhotographerOrMissing_ThrowNotFound()
    {
        var settlement = Pending(Guid.CreateVersion7());
        _settlements.GetByBookingIdAsync(settlement.BookingId, Arg.Any<CancellationToken>()).Returns(settlement);
        var stranger = Guid.CreateVersion7();
        var token = TestContext.Current.CancellationToken;

        await Should.ThrowAsync<NotFoundException>(
            () => new GetSettlementHandler(_settlements).HandleAsync(new GetSettlementQuery(stranger, settlement.BookingId), token));
        await Should.ThrowAsync<NotFoundException>(
            () => new GetSettlementHandler(_settlements).HandleAsync(new GetSettlementQuery(settlement.PhotographerId, Guid.CreateVersion7()), token));
        await Should.ThrowAsync<NotFoundException>(
            () => new CompleteRefundHandler(_settlements, new FixedTimeProvider(Now)).HandleAsync(
                new CompleteRefundCommand(stranger, settlement.BookingId, PaymentMethod.Cash, null), token));
        settlement.RefundStatus.ShouldBe(RefundStatus.Pending);
    }

    /// <summary>
    /// The list returns the pending refunds and how many there are.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListRefunds_ReturnsThePendingOnesAndTheCount()
    {
        var photographer = Guid.CreateVersion7();
        _settlements.ListPendingRefundsAsync(photographer, ListRefundsHandler.MaxItems, Arg.Any<CancellationToken>()).Returns([Pending(photographer)]);
        _settlements.CountPendingRefundsAsync(photographer, Arg.Any<CancellationToken>()).Returns(1);

        var response = await new ListRefundsHandler(_settlements).HandleAsync(new ListRefundsQuery(photographer), TestContext.Current.CancellationToken);

        response.PendingCount.ShouldBe(1);
        response.Items.Count.ShouldBe(1);
    }

    /// <summary>
    /// Completing a refund stores it and returns the updated settlement.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CompleteRefund_RecordsTheRefund()
    {
        var settlement = Pending(Guid.CreateVersion7());
        _settlements.GetByBookingIdAsync(settlement.BookingId, Arg.Any<CancellationToken>()).Returns(settlement);

        var response = await new CompleteRefundHandler(_settlements, new FixedTimeProvider(Now.AddHours(1))).HandleAsync(
            new CompleteRefundCommand(settlement.PhotographerId, settlement.BookingId, PaymentMethod.SinpeMovil, "ref 1"),
            TestContext.Current.CancellationToken);

        response.RefundStatus.ShouldBe(RefundStatus.Completed);
        response.RefundMethod.ShouldBe(PaymentMethod.SinpeMovil);
        response.RefundNote.ShouldBe("ref 1");
        await _settlements.Received(1).UpdateAsync(settlement, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A refund already completed cannot be completed again, and nothing is saved.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CompleteRefund_Twice_IsRejectedByTheDomain()
    {
        var settlement = Pending(Guid.CreateVersion7());
        settlement.CompleteRefund(PaymentMethod.Cash, null, Now);
        _settlements.GetByBookingIdAsync(settlement.BookingId, Arg.Any<CancellationToken>()).Returns(settlement);

        await Should.ThrowAsync<DomainException>(
            () => new CompleteRefundHandler(_settlements, new FixedTimeProvider(Now)).HandleAsync(
                new CompleteRefundCommand(settlement.PhotographerId, settlement.BookingId, PaymentMethod.Cash, null),
                TestContext.Current.CancellationToken));
        await _settlements.DidNotReceiveWithAnyArgs().UpdateAsync(default!, TestContext.Current.CancellationToken);
    }
}
