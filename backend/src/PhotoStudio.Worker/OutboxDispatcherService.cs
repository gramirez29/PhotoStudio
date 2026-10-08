using MongoDB.Driver;
using PhotoStudio.Infrastructure.Persistence.Outbox;

namespace PhotoStudio.Worker;

/// <summary>
/// Keeps delivering the pending outbox messages. It polls: with a full batch it continues immediately, otherwise it waits
/// <see cref="PollInterval"/>. A MongoDB outage is logged and retried on the next cycle; it never stops the worker.
/// </summary>
/// <param name="processor">Delivers the messages.</param>
/// <param name="timeProvider">Clock abstraction used for the waits.</param>
/// <param name="logger">Logger.</param>
public sealed partial class OutboxDispatcherService(
    OutboxProcessor processor,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcherService> logger) : BackgroundService
{
    /// <summary>
    /// Messages delivered per poll.
    /// </summary>
    public const int BatchSize = 50;

    /// <summary>
    /// Wait between polls when no full batch was found.
    /// </summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Runs the polling loop until the host stops.
    /// </summary>
    /// <param name="stoppingToken">Signaled when the host is shutting down.</param>
    /// <returns>A task that completes when the loop ends.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var claimed = 0;
            try
            {
                claimed = await processor.ProcessBatchAsync(BatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is MongoException or TimeoutException)
            {
                LogPollFailed(logger, exception);
            }

            if (claimed < BatchSize)
            {
                await Task.Delay(PollInterval, timeProvider, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    /// <summary>
    /// Logs a poll that failed because MongoDB did not answer.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="exception">Failure cause.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The outbox poll failed; it will be retried on the next cycle.")]
    private static partial void LogPollFailed(ILogger logger, Exception exception);
}
