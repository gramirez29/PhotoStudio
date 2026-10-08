using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.ExpireTentativeBookings;

namespace PhotoStudio.Application.Maintenance.RunMaintenance;

/// <summary>
/// Runs one maintenance pass. Expiration goes first so the <c>BookingExpired</c> events it raises are delivered in the same
/// pass. Each task repeats in batches until nothing is left, up to <see cref="MaxBatches"/> batches, so one pass has a
/// bounded duration even with a large backlog; when the limit is reached the response says more work is pending.
/// </summary>
/// <param name="expiration">Handler that expires tentative bookings.</param>
/// <param name="outbox">Delivers the pending outbox messages.</param>
public sealed class RunMaintenanceHandler(
    ICommandHandler<ExpireTentativeBookingsCommand, ExpireTentativeBookingsResult> expiration,
    IOutboxDispatcher outbox) : ICommandHandler<RunMaintenanceCommand, MaintenanceResponse>
{
    /// <summary>
    /// Bookings examined per expiration batch.
    /// </summary>
    public const int ExpirationBatchSize = 100;

    /// <summary>
    /// Messages delivered per outbox batch.
    /// </summary>
    public const int OutboxBatchSize = 50;

    /// <summary>
    /// Maximum batches per task in one pass.
    /// </summary>
    public const int MaxBatches = 10;

    /// <summary>
    /// Runs the pass.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>What the pass did.</returns>
    public async Task<MaintenanceResponse> HandleAsync(RunMaintenanceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var expired = 0;
        var skipped = 0;
        var morePending = false;
        for (var batch = 1; batch <= MaxBatches; batch++)
        {
            var result = await expiration.HandleAsync(new ExpireTentativeBookingsCommand(ExpirationBatchSize), cancellationToken);
            expired += result.Expired;
            skipped += result.Skipped;

            // A short batch means nothing is left. A batch without a single expiration would only repeat itself.
            if (result.Expired == 0 || result.Expired + result.Skipped < ExpirationBatchSize)
            {
                break;
            }

            morePending |= batch == MaxBatches;
        }

        var processed = 0;
        for (var batch = 1; batch <= MaxBatches; batch++)
        {
            var claimed = await outbox.ProcessBatchAsync(OutboxBatchSize, cancellationToken);
            processed += claimed;

            if (claimed < OutboxBatchSize)
            {
                break;
            }

            morePending |= batch == MaxBatches;
        }

        return new MaintenanceResponse(expired, skipped, processed, morePending);
    }
}
