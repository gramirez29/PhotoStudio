namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Port to the outbox: delivers the pending domain events to their handlers. Implemented in the Infrastructure layer, so
/// the application layer can trigger a delivery pass without knowing how messages are stored.
/// </summary>
public interface IOutboxDispatcher
{
    /// <summary>
    /// Claims and delivers up to <paramref name="batchSize"/> due messages, oldest first.
    /// </summary>
    /// <param name="batchSize">Maximum number of messages to deliver in this call.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of messages that were claimed, whether or not their delivery succeeded.</returns>
    Task<int> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken);
}
