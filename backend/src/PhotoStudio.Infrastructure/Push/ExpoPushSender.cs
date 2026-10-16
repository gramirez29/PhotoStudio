using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Infrastructure.Push;

/// <summary>
/// Settings of the Expo push service, read from environment variables (Railway service variables in production).
/// </summary>
/// <param name="AccessToken">Expo access token, or <see langword="null"/> when the project does not require one.</param>
public sealed record ExpoPushOptions(string? AccessToken)
{
    /// <summary>Environment variable that holds the optional Expo access token. Sending works without it unless the Expo project enabled enhanced push security.</summary>
    public const string AccessTokenVariable = "EXPO_ACCESS_TOKEN";

    /// <summary>
    /// Reads the options from the process environment.
    /// </summary>
    /// <returns>The options.</returns>
    public static ExpoPushOptions FromEnvironment()
    {
        var token = Environment.GetEnvironmentVariable(AccessTokenVariable);
        return new ExpoPushOptions(string.IsNullOrWhiteSpace(token) ? null : token.Trim());
    }
}

/// <summary>
/// Sends push notifications through the Expo push service, which relays them to Firebase (Android) or Apple. The service takes
/// up to 100 messages per request and answers with one ticket per message, in order. A ticket that says the device is no
/// longer registered (the app was uninstalled) is reported so the token can be forgotten. A provider that fails or cannot be
/// reached is logged and not thrown, because the push is a convenience on top of the inbox. Delivery receipts, which the
/// provider makes available later, are not read.
/// </summary>
/// <param name="httpClient">HTTP client pointed at Expo.</param>
/// <param name="options">Provider settings.</param>
/// <param name="logger">Logger.</param>
public sealed partial class ExpoPushSender(HttpClient httpClient, ExpoPushOptions options, ILogger<ExpoPushSender> logger) : IPushSender
{
    /// <summary>
    /// Address of the Expo push service.
    /// </summary>
    public const string BaseAddress = "https://exp.host/";

    /// <summary>
    /// Maximum number of messages per request, a limit of the service.
    /// </summary>
    public const int MaxMessagesPerRequest = 100;

    /// <summary>
    /// Path of the send endpoint.
    /// </summary>
    private const string SendPath = "--/api/v2/push/send";

    /// <summary>
    /// Error code the service uses for a token whose app was uninstalled.
    /// </summary>
    private const string DeviceNotRegistered = "DeviceNotRegistered";

    /// <inheritdoc />
    public async Task<PushSendResult> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var invalid = new List<string>();
        foreach (var batch in messages.Chunk(MaxMessagesPerRequest))
        {
            try
            {
                invalid.AddRange(await SendBatchAsync(batch, cancellationToken));
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                // Not thrown: see the class summary. A cancellation requested by the caller is not swallowed.
                cancellationToken.ThrowIfCancellationRequested();
                LogBatchFailed(logger, batch.Length, exception);
            }
        }

        return new PushSendResult(invalid);
    }

    /// <summary>
    /// Sends one batch and reads its tickets.
    /// </summary>
    /// <param name="batch">At most <see cref="MaxMessagesPerRequest"/> messages.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The tokens of the batch that are no longer valid.</returns>
    private async Task<IReadOnlyList<string>> SendBatchAsync(PushMessage[] batch, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, SendPath)
        {
            Content = JsonContent.Create(batch.Select(message => new
            {
                to = message.Token,
                title = message.Title,
                body = message.Body,
                data = message.Data,
                sound = "default",
                priority = "high",
                channelId = "default",
            })),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (options.AccessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogRefused(logger, (int)response.StatusCode, batch.Length);
            return [];
        }

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!body.RootElement.TryGetProperty("data", out var tickets) || tickets.ValueKind != JsonValueKind.Array)
        {
            LogUnexpectedAnswer(logger, batch.Length);
            return [];
        }

        var invalid = new List<string>();
        var index = 0;
        foreach (var ticket in tickets.EnumerateArray())
        {
            if (index >= batch.Length)
            {
                break;
            }

            if (ticket.TryGetProperty("status", out var status) && status.GetString() == "error")
            {
                var error = ticket.TryGetProperty("details", out var details) && details.TryGetProperty("error", out var code)
                    ? code.GetString()
                    : null;
                if (error == DeviceNotRegistered)
                {
                    invalid.Add(batch[index].Token);
                }
                else
                {
                    LogTicketError(logger, error ?? "unknown");
                }
            }

            index++;
        }

        return invalid;
    }

    /// <summary>
    /// Logs a batch that could not be sent.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="count">Messages in the batch.</param>
    /// <param name="exception">What failed.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Expo could not be reached; {Count} push messages were not sent.")]
    private static partial void LogBatchFailed(ILogger logger, int count, Exception exception);

    /// <summary>
    /// Logs a batch that Expo refused.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="status">HTTP status of the answer.</param>
    /// <param name="count">Messages in the batch.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Expo refused the push request (HTTP {Status}); {Count} messages were not sent.")]
    private static partial void LogRefused(ILogger logger, int status, int count);

    /// <summary>
    /// Logs an answer that does not have the expected shape.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="count">Messages in the batch.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Expo answered a push request of {Count} messages with an unexpected body.")]
    private static partial void LogUnexpectedAnswer(ILogger logger, int count);

    /// <summary>
    /// Logs a ticket that reports an error other than an uninstalled app.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="error">Error code of the ticket.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Expo reported an error for a push message: {Error}.")]
    private static partial void LogTicketError(ILogger logger, string error);
}
