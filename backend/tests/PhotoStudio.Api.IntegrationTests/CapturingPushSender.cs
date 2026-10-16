using System.Collections.Concurrent;
using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// Push sender for the tests: keeps every message instead of sending it to Expo, so a test can check what the photographer's
/// phone would have received.
/// </summary>
public sealed class CapturingPushSender : IPushSender
{
    private readonly ConcurrentQueue<PushMessage> _messages = new();

    /// <summary>
    /// Gets the messages captured so far, oldest first.
    /// </summary>
    public IReadOnlyCollection<PushMessage> Messages => _messages;

    /// <inheritdoc />
    public Task<PushSendResult> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        foreach (var message in messages)
        {
            _messages.Enqueue(message);
        }

        return Task.FromResult(new PushSendResult([]));
    }
}
