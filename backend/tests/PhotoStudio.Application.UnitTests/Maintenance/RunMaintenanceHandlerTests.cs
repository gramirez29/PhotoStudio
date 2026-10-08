using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.ExpireTentativeBookings;
using PhotoStudio.Application.Maintenance.RunMaintenance;

namespace PhotoStudio.Application.UnitTests.Maintenance;

/// <summary>
/// Tests of <see cref="RunMaintenanceHandler"/>.
/// </summary>
public sealed class RunMaintenanceHandlerTests
{
    private readonly ICommandHandler<ExpireTentativeBookingsCommand, ExpireTentativeBookingsResult> _expiration =
        Substitute.For<ICommandHandler<ExpireTentativeBookingsCommand, ExpireTentativeBookingsResult>>();

    private readonly IOutboxDispatcher _outbox = Substitute.For<IOutboxDispatcher>();

    /// <summary>
    /// With nothing to do, both tasks run once and the response is empty.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithNothingToDo_RunsEachTaskOnce()
    {
        ExpirationReturns(new ExpireTentativeBookingsResult(0, 0));
        _outbox.ProcessBatchAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0);

        var response = await NewHandler().HandleAsync(new RunMaintenanceCommand(), TestContext.Current.CancellationToken);

        response.ShouldBe(new MaintenanceResponse(0, 0, 0, false));
        await _expiration.Received(1).HandleAsync(Arg.Any<ExpireTentativeBookingsCommand>(), Arg.Any<CancellationToken>());
        await _outbox.Received(1).ProcessBatchAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Expiration runs before the outbox, so the events it raises are delivered in the same pass.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_ExpiresBeforeDeliveringTheOutbox()
    {
        var order = new List<string>();
        _expiration.HandleAsync(Arg.Any<ExpireTentativeBookingsCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("expiration");
                return new ExpireTentativeBookingsResult(1, 0);
            });
        _outbox.ProcessBatchAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("outbox");
                return 1;
            });

        var response = await NewHandler().HandleAsync(new RunMaintenanceCommand(), TestContext.Current.CancellationToken);

        order.ShouldBe(["expiration", "outbox"]);
        response.ShouldBe(new MaintenanceResponse(1, 0, 1, false));
    }

    /// <summary>
    /// A full batch means more may be waiting, so the task runs again until a short batch arrives; totals are added up.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithFullBatches_RepeatsUntilAShortBatch()
    {
        _expiration.HandleAsync(Arg.Any<ExpireTentativeBookingsCommand>(), Arg.Any<CancellationToken>())
            .Returns(
                new ExpireTentativeBookingsResult(RunMaintenanceHandler.ExpirationBatchSize, 0),
                new ExpireTentativeBookingsResult(30, 2));
        _outbox.ProcessBatchAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(RunMaintenanceHandler.OutboxBatchSize, 7);

        var response = await NewHandler().HandleAsync(new RunMaintenanceCommand(), TestContext.Current.CancellationToken);

        response.ShouldBe(new MaintenanceResponse(RunMaintenanceHandler.ExpirationBatchSize + 30, 2, RunMaintenanceHandler.OutboxBatchSize + 7, false));
    }

    /// <summary>
    /// A backlog larger than the pass limit stops at the limit and reports that more work is pending.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithABacklogLargerThanTheLimit_StopsAndReportsMoreWork()
    {
        ExpirationReturns(new ExpireTentativeBookingsResult(RunMaintenanceHandler.ExpirationBatchSize, 0));
        _outbox.ProcessBatchAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0);

        var response = await NewHandler().HandleAsync(new RunMaintenanceCommand(), TestContext.Current.CancellationToken);

        response.MoreWorkPending.ShouldBeTrue();
        response.BookingsExpired.ShouldBe(RunMaintenanceHandler.ExpirationBatchSize * RunMaintenanceHandler.MaxBatches);
        await _expiration.Received(RunMaintenanceHandler.MaxBatches)
            .HandleAsync(Arg.Any<ExpireTentativeBookingsCommand>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A batch where every booking was skipped would only repeat itself, so the task stops without reporting pending work.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenEveryBookingIsSkipped_StopsInsteadOfRepeating()
    {
        ExpirationReturns(new ExpireTentativeBookingsResult(0, RunMaintenanceHandler.ExpirationBatchSize));
        _outbox.ProcessBatchAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0);

        var response = await NewHandler().HandleAsync(new RunMaintenanceCommand(), TestContext.Current.CancellationToken);

        response.ShouldBe(new MaintenanceResponse(0, RunMaintenanceHandler.ExpirationBatchSize, 0, false));
        await _expiration.Received(1).HandleAsync(Arg.Any<ExpireTentativeBookingsCommand>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Makes the expiration handler always return the same result.
    /// </summary>
    /// <param name="result">Result to return.</param>
    private void ExpirationReturns(ExpireTentativeBookingsResult result) =>
        _expiration.HandleAsync(Arg.Any<ExpireTentativeBookingsCommand>(), Arg.Any<CancellationToken>()).Returns(result);

    /// <summary>
    /// Creates the handler under test.
    /// </summary>
    /// <returns>The handler.</returns>
    private RunMaintenanceHandler NewHandler() => new(_expiration, _outbox);
}
