using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.ExpireTentativeBookings;

namespace PhotoStudio.Worker;

/// <summary>
/// Runs the B7 expiration job every <see cref="RunInterval"/>. The handler re-evaluates the domain guards on each run, so
/// the job is idempotent. A MongoDB outage is logged and retried on the next cycle; it never stops the worker.
/// </summary>
/// <param name="scopeFactory">Creates the scope in which the handler runs.</param>
/// <param name="timeProvider">Clock abstraction used for the waits.</param>
/// <param name="logger">Logger.</param>
public sealed partial class BookingExpirationService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<BookingExpirationService> logger) : BackgroundService
{
    /// <summary>
    /// Bookings examined per run.
    /// </summary>
    public const int BatchSize = 100;

    /// <summary>
    /// Wait between runs.
    /// </summary>
    public static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Runs the job until the host stops.
    /// </summary>
    /// <param name="stoppingToken">Signaled when the host is shutting down.</param>
    /// <returns>A task that completes when the loop ends.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider
                    .GetRequiredService<ICommandHandler<ExpireTentativeBookingsCommand, ExpireTentativeBookingsResult>>();

                var result = await handler.HandleAsync(new ExpireTentativeBookingsCommand(BatchSize), stoppingToken);
                if (result.Expired > 0 || result.Skipped > 0)
                {
                    LogRunCompleted(logger, result.Expired, result.Skipped);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is MongoException or TimeoutException)
            {
                LogRunFailed(logger, exception);
            }

            await Task.Delay(RunInterval, timeProvider, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    /// <summary>
    /// Logs the outcome of a run that touched bookings.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="expired">Bookings expired.</param>
    /// <param name="skipped">Bookings skipped.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Booking expiration run: {Expired} expired, {Skipped} skipped.")]
    private static partial void LogRunCompleted(ILogger logger, int expired, int skipped);

    /// <summary>
    /// Logs a run that failed because MongoDB did not answer.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="exception">Failure cause.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The booking expiration run failed; it will be retried on the next cycle.")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);
}
