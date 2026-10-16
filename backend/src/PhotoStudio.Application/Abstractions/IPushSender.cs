namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// A push notification for one device.
/// </summary>
/// <param name="Token">Expo push token of the device.</param>
/// <param name="Title">Title the system shows.</param>
/// <param name="Body">Text the system shows.</param>
/// <param name="Data">Values the app receives when the notification is tapped.</param>
public sealed record PushMessage(string Token, string Title, string Body, IReadOnlyDictionary<string, string> Data);

/// <summary>
/// Outcome of a push send.
/// </summary>
/// <param name="InvalidTokens">Tokens the push service reported as no longer valid, which should be forgotten.</param>
public sealed record PushSendResult(IReadOnlyList<string> InvalidTokens);

/// <summary>
/// Port that sends push notifications to phones. Implemented in the Infrastructure layer, so the provider (Expo today) can
/// change without touching the use cases.
/// </summary>
public interface IPushSender
{
    /// <summary>
    /// Sends the messages. A provider that cannot be reached is not an error for the caller: the push is a convenience on
    /// top of the inbox, which already holds the notice, so the implementation reports the problem in the log and answers
    /// as if nothing was invalid.
    /// </summary>
    /// <param name="messages">Messages to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The tokens that are no longer valid.</returns>
    Task<PushSendResult> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken);
}
