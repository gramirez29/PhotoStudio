using Microsoft.Extensions.Logging;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.Notifications.Push;

/// <summary>
/// Sends a push to the phones of the photographer for each notification that reached the inbox. The push is a convenience on
/// top of the inbox, so nothing here can make a delivery fail: a problem is logged and the notification stays delivered. It
/// is called once per delivered notification (the delivery wins a version check first), so a notice is not pushed twice.
/// </summary>
/// <param name="devices">Push device repository.</param>
/// <param name="sender">Push sender.</param>
/// <param name="logger">Logger.</param>
public sealed partial class NotificationPusher(IPushDeviceRepository devices, IPushSender sender, ILogger<NotificationPusher> logger)
{
    /// <summary>
    /// Pushes the notifications that were just delivered. Tokens the provider rejects are forgotten.
    /// </summary>
    /// <param name="delivered">Notifications that reached the inbox.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the push was attempted.</returns>
    public async Task PushAsync(IReadOnlyCollection<Notification> delivered, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivered);

        try
        {
            var messages = new List<PushMessage>();
            foreach (var group in delivered.GroupBy(notification => notification.PhotographerId))
            {
                var photographerDevices = await devices.ListByPhotographerAsync(group.Key, cancellationToken);
                foreach (var notification in group)
                {
                    var (title, body) = NotificationPushText.Describe(notification);
                    var data = new Dictionary<string, string>
                    {
                        ["notificationId"] = notification.Id.ToString(),
                        ["bookingId"] = notification.BookingId.ToString(),
                        ["type"] = notification.Type.ToString(),
                    };
                    messages.AddRange(photographerDevices.Select(device => new PushMessage(device.Token, title, body, data)));
                }
            }

            if (messages.Count == 0)
            {
                return;
            }

            var result = await sender.SendAsync(messages, cancellationToken);
            if (result.InvalidTokens.Count > 0)
            {
                await devices.DeleteTokensAsync(result.InvalidTokens, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPushFailed(logger, exception);
        }
    }

    /// <summary>
    /// Logs a push that could not be sent.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="exception">What failed.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The push for delivered notifications failed; the notices are still in the inbox.")]
    private static partial void LogPushFailed(ILogger logger, Exception exception);
}
