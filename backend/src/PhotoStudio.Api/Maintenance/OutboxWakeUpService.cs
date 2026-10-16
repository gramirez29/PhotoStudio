using System.Threading.Channels;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Maintenance.RunMaintenance;

namespace PhotoStudio.Api.Maintenance;

/// <summary>
/// Runs a maintenance pass soon after a booking changes, so what the change causes (a push for a proof of payment, a
/// reminder that is already due) reaches the photographer in seconds instead of waiting for the next scheduled run of the
/// worker, which can be 15 minutes away. It is only a shortcut: the worker still delivers everything, and the pass is
/// idempotent and safe to run in two processes at once (messages are claimed with a lease and notifications with a version
/// check). Signals that arrive while a pass runs are merged into a single following pass.
/// </summary>
/// <param name="scopeFactory">Creates the scope each pass runs in.</param>
/// <param name="logger">Logger.</param>
public sealed partial class OutboxWakeUpService(IServiceScopeFactory scopeFactory, ILogger<OutboxWakeUpService> logger)
    : BackgroundService, IOutboxWakeUp
{
    /// <summary>
    /// Time to wait after a pass before the next one can start, so a burst of requests does not become a burst of passes.
    /// </summary>
    private static readonly TimeSpan MinimumGap = TimeSpan.FromSeconds(1);

    // One pending signal is enough: it means "run once more", however many requests asked for it.
    private readonly Channel<bool> _signals = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    /// <inheritdoc />
    public void Signal() => _signals.Writer.TryWrite(true);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await _signals.Reader.WaitToReadAsync(stoppingToken))
            {
                _signals.Reader.TryRead(out _);
                await RunPassAsync(stoppingToken);
                await Task.Delay(MinimumGap, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The host is stopping.
        }
    }

    /// <summary>
    /// Runs one pass. A failure (for example MongoDB being unreachable) is logged and does not stop the service: the next
    /// signal, or the worker, tries again.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels when the host stops.</param>
    /// <returns>A task that completes when the pass ends.</returns>
    private async Task RunPassAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<RunMaintenanceCommand, MaintenanceResponse>>();
            await handler.HandleAsync(new RunMaintenanceCommand(), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPassFailed(logger, exception);
        }
    }

    /// <summary>
    /// Logs a pass that failed.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="exception">What failed.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The maintenance pass triggered by a booking change failed; the worker will retry.")]
    private static partial void LogPassFailed(ILogger logger, Exception exception);
}
