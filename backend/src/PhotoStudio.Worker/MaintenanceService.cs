using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Maintenance.RunMaintenance;

namespace PhotoStudio.Worker;

/// <summary>
/// Runs the maintenance pass (expire bookings, deliver the outbox, deliver due notifications) every
/// <see cref="RunInterval"/> while the worker stays alive. It is the same pass the scheduled run-once mode and the on-demand
/// endpoint execute, so all three behave alike; this one only repeats it. Every step is idempotent. A MongoDB outage is
/// logged and retried on the next cycle; it never stops the worker.
/// </summary>
/// <param name="scopeFactory">Creates the scope in which the handler runs.</param>
/// <param name="timeProvider">Clock abstraction used for the waits.</param>
/// <param name="logger">Logger.</param>
public sealed partial class MaintenanceService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<MaintenanceService> logger) : BackgroundService
{
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
                    .GetRequiredService<ICommandHandler<RunMaintenanceCommand, MaintenanceResponse>>();

                var result = await handler.HandleAsync(new RunMaintenanceCommand(), stoppingToken);
                if (result.BookingsExpired > 0 || result.BookingsSkipped > 0 || result.NotificationsDelivered > 0)
                {
                    LogRunCompleted(logger, result.BookingsExpired, result.BookingsSkipped, result.NotificationsDelivered);
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
    /// Logs the outcome of a run that changed something.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="expired">Bookings expired.</param>
    /// <param name="skipped">Bookings skipped.</param>
    /// <param name="notifications">Notifications delivered.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Maintenance run: {Expired} bookings expired, {Skipped} skipped, {Notifications} notifications delivered.")]
    private static partial void LogRunCompleted(ILogger logger, int expired, int skipped, int notifications);

    /// <summary>
    /// Logs a run that failed because MongoDB did not answer.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="exception">Failure cause.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The maintenance run failed; it will be retried on the next cycle.")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);
}
