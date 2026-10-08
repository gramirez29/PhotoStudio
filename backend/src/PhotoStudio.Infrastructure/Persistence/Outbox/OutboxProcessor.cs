using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Common;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence.Outbox;

/// <summary>
/// Delivers the pending outbox messages to the registered <see cref="IDomainEventHandler{TEvent}"/> implementations.
/// A message is claimed with a lease (<see cref="LeaseDuration"/>) before it is delivered, so two workers never deliver the
/// same message at once and a message held by a crashed worker is picked up again when its lease ends. Delivery is
/// at-least-once: a handler may see the same event twice and must be idempotent. Failed deliveries are retried with
/// exponential backoff up to <see cref="MaxAttempts"/> times, then the message is parked as failed for manual review.
/// </summary>
/// <param name="database">MongoDB database.</param>
/// <param name="scopeFactory">Creates the scope in which the handlers of each message are resolved.</param>
/// <param name="timeProvider">Clock abstraction.</param>
/// <param name="logger">Logger.</param>
public sealed partial class OutboxProcessor(
    IMongoDatabase database,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger) : IOutboxDispatcher
{
    /// <summary>
    /// Name of the outbox collection.
    /// </summary>
    public const string CollectionName = "outbox_messages";

    /// <summary>
    /// Deliveries attempted before a message is parked as failed.
    /// </summary>
    public const int MaxAttempts = 8;

    /// <summary>
    /// How long a worker holds a claimed message before another worker may take it.
    /// </summary>
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(15);

    private readonly IMongoCollection<OutboxMessageDocument> _messages = database.GetCollection<OutboxMessageDocument>(CollectionName);

    /// <inheritdoc />
    public async Task<int> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        var claimed = 0;
        while (claimed < batchSize && !cancellationToken.IsCancellationRequested)
        {
            var message = await ClaimNextAsync(cancellationToken);
            if (message is null)
            {
                break;
            }

            claimed++;
            await DeliverAsync(message, cancellationToken);
        }

        return claimed;
    }

    /// <summary>
    /// Atomically takes the oldest due message, stamping its lease and counting the attempt.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The claimed message, or <see langword="null"/> when nothing is due.</returns>
    private async Task<OutboxMessageDocument?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var filter = Builders<OutboxMessageDocument>.Filter;

        return await _messages.FindOneAndUpdateAsync(
            filter.And(
                filter.Eq(message => message.Status, OutboxMessageDocument.PendingStatus),
                filter.Lte(message => message.NextAttemptAt, now),
                filter.Or(
                    filter.Eq(message => message.LockedUntil, null),
                    filter.Lte(message => message.LockedUntil, now))),
            Builders<OutboxMessageDocument>.Update
                .Set(message => message.LockedUntil, now + LeaseDuration)
                .Inc(message => message.Attempts, 1),
            new FindOneAndUpdateOptions<OutboxMessageDocument>
            {
                Sort = Builders<OutboxMessageDocument>.Sort.Ascending(message => message.OccurredAt).Ascending(message => message.Id),
                ReturnDocument = ReturnDocument.After,
            },
            cancellationToken);
    }

    /// <summary>
    /// Runs the handlers of one claimed message and records the outcome.
    /// </summary>
    /// <param name="message">Message returned by <see cref="ClaimNextAsync"/>.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the outcome is stored.</returns>
    private async Task DeliverAsync(OutboxMessageDocument message, CancellationToken cancellationToken)
    {
        IDomainEvent domainEvent;
        try
        {
            domainEvent = DomainEventSerializer.Deserialize(message.Type, message.Payload);
        }
        catch (InvalidOperationException exception)
        {
            // Retrying cannot fix an event that cannot be read.
            LogMessageUnreadable(logger, message.Id, message.Type, exception);
            await ParkAsync(message, exception.Message, cancellationToken);
            return;
        }

        try
        {
            await InvokeHandlersAsync(domainEvent, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down: leave the lease to expire so the message is delivered again by the next worker.
            throw;
        }
        catch (Exception exception)
        {
            await RegisterFailureAsync(message, exception, cancellationToken);
            return;
        }

        await _messages.UpdateOneAsync(
            stored => stored.Id == message.Id,
            Builders<OutboxMessageDocument>.Update
                .Set(stored => stored.Status, OutboxMessageDocument.ProcessedStatus)
                .Set(stored => stored.ProcessedAt, timeProvider.GetUtcNow().UtcDateTime)
                .Set(stored => stored.LastError, null)
                .Unset(stored => stored.LockedUntil),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Resolves every handler registered for the event inside a fresh scope and runs them one after another.
    /// </summary>
    /// <param name="domainEvent">Event to deliver.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when every handler finished.</returns>
    private async Task InvokeHandlersAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;

        await using var scope = scopeFactory.CreateAsyncScope();
        foreach (var handler in scope.ServiceProvider.GetServices(handlerType))
        {
            try
            {
                await (Task)handleMethod.Invoke(handler, [domainEvent, cancellationToken])!;
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                // Reflection wraps what the handler threw; surface the original error.
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }
    }

    /// <summary>
    /// Schedules the next attempt with exponential backoff, or parks the message once it ran out of attempts.
    /// </summary>
    /// <param name="message">Message whose delivery failed.</param>
    /// <param name="exception">Why it failed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the outcome is stored.</returns>
    private async Task RegisterFailureAsync(OutboxMessageDocument message, Exception exception, CancellationToken cancellationToken)
    {
        if (message.Attempts >= MaxAttempts)
        {
            LogMessageParked(logger, message.Id, message.Type, message.Attempts, exception);
            await ParkAsync(message, exception.Message, cancellationToken);
            return;
        }

        var delay = TimeSpan.FromTicks(Math.Min(
            MaxRetryDelay.Ticks,
            BaseRetryDelay.Ticks * (1L << (message.Attempts - 1))));
        LogDeliveryFailed(logger, message.Id, message.Type, message.Attempts, delay, exception);

        await _messages.UpdateOneAsync(
            stored => stored.Id == message.Id,
            Builders<OutboxMessageDocument>.Update
                .Set(stored => stored.NextAttemptAt, timeProvider.GetUtcNow().UtcDateTime + delay)
                .Set(stored => stored.LastError, exception.Message)
                .Unset(stored => stored.LockedUntil),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Marks a message as failed so it is no longer claimed.
    /// </summary>
    /// <param name="message">Message to park.</param>
    /// <param name="error">Reason, kept for manual review.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the message is parked.</returns>
    private Task ParkAsync(OutboxMessageDocument message, string error, CancellationToken cancellationToken) =>
        _messages.UpdateOneAsync(
            stored => stored.Id == message.Id,
            Builders<OutboxMessageDocument>.Update
                .Set(stored => stored.Status, OutboxMessageDocument.FailedStatus)
                .Set(stored => stored.LastError, error)
                .Unset(stored => stored.LockedUntil),
            cancellationToken: cancellationToken);

    /// <summary>
    /// Logs a delivery that will be retried.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="messageId">Outbox message identifier.</param>
    /// <param name="eventType">Event type name.</param>
    /// <param name="attempts">Attempts made so far.</param>
    /// <param name="delay">Wait before the next attempt.</param>
    /// <param name="exception">Failure cause.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} ({EventType}) failed on attempt {Attempts}; retrying in {Delay}.")]
    private static partial void LogDeliveryFailed(ILogger logger, Guid messageId, string eventType, int attempts, TimeSpan delay, Exception exception);

    /// <summary>
    /// Logs a message that exhausted its attempts.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="messageId">Outbox message identifier.</param>
    /// <param name="eventType">Event type name.</param>
    /// <param name="attempts">Attempts made.</param>
    /// <param name="exception">Failure cause.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} ({EventType}) failed after {Attempts} attempts and was parked for manual review.")]
    private static partial void LogMessageParked(ILogger logger, Guid messageId, string eventType, int attempts, Exception exception);

    /// <summary>
    /// Logs a message that cannot be deserialized.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="messageId">Outbox message identifier.</param>
    /// <param name="eventType">Event type name.</param>
    /// <param name="exception">Failure cause.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} ({EventType}) cannot be read and was parked for manual review.")]
    private static partial void LogMessageUnreadable(ILogger logger, Guid messageId, string eventType, Exception exception);
}
