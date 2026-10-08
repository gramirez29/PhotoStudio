using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Maintenance.RunMaintenance;

namespace PhotoStudio.Worker;

/// <summary>
/// "Run once" mode of the worker, meant for a scheduled job (Railway cron): it performs one maintenance pass and exits, so
/// the service only runs, and only costs, for the few seconds the pass takes. It is selected with the
/// <see cref="EnvironmentVariable"/> variable; without it the worker stays alive and polls (see
/// <see cref="OutboxDispatcherService"/> and <see cref="BookingExpirationService"/>).
/// </summary>
public static partial class RunOnceJob
{
    /// <summary>
    /// Name of the environment variable that turns the run-once mode on when its value is <c>true</c>.
    /// </summary>
    public const string EnvironmentVariable = "WORKER_RUN_ONCE";

    /// <summary>
    /// Process exit code of a successful pass.
    /// </summary>
    public const int SuccessExitCode = 0;

    /// <summary>
    /// Process exit code of a pass that could not reach MongoDB; the scheduler shows the run as failed.
    /// </summary>
    public const int DatabaseUnavailableExitCode = 1;

    /// <summary>
    /// Indicates whether the run-once mode is selected in the current environment.
    /// </summary>
    /// <returns><see langword="true"/> when <see cref="EnvironmentVariable"/> is <c>true</c> (case-insensitive).</returns>
    public static bool IsEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable(EnvironmentVariable), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Runs one maintenance pass and reports the outcome in the logs.
    /// </summary>
    /// <param name="services">Service provider of the started host.</param>
    /// <param name="cancellationToken">Token that is cancelled when the host is asked to stop.</param>
    /// <returns>The process exit code: <see cref="SuccessExitCode"/> or <see cref="DatabaseUnavailableExitCode"/>.</returns>
    public static async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("PhotoStudio.Worker.RunOnce");

        try
        {
            await using var scope = services.CreateAsyncScope();
            var handler = scope.ServiceProvider
                .GetRequiredService<ICommandHandler<RunMaintenanceCommand, MaintenanceResponse>>();

            var result = await handler.HandleAsync(new RunMaintenanceCommand(), cancellationToken);
            LogPassCompleted(logger, result.BookingsExpired, result.BookingsSkipped, result.EventsProcessed);
            if (result.MoreWorkPending)
            {
                LogMoreWorkPending(logger);
            }

            return SuccessExitCode;
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            LogPassFailed(logger, exception);
            return DatabaseUnavailableExitCode;
        }
    }

    /// <summary>
    /// Logs the outcome of a pass.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="expired">Bookings expired.</param>
    /// <param name="skipped">Bookings skipped.</param>
    /// <param name="events">Outbox messages processed.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Maintenance pass completed: {Expired} bookings expired, {Skipped} skipped, {Events} events processed.")]
    private static partial void LogPassCompleted(ILogger logger, int expired, int skipped, int events);

    /// <summary>
    /// Logs that the pass stopped at its size limit.
    /// </summary>
    /// <param name="logger">Logger.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The maintenance pass stopped at its size limit; more work is pending and the next pass will continue.")]
    private static partial void LogMoreWorkPending(ILogger logger);

    /// <summary>
    /// Logs a pass that failed because MongoDB did not answer.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="exception">Failure cause.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "The maintenance pass failed because MongoDB did not answer.")]
    private static partial void LogPassFailed(ILogger logger, Exception exception);
}
