using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;

namespace PhotoStudio.Infrastructure.Email;

/// <summary>
/// Settings of the Resend email provider, read from environment variables (Railway service variables in production).
/// The API key is a secret and must never be committed.
/// </summary>
/// <param name="ApiKey">Resend API key, restricted to sending.</param>
/// <param name="From">Sender shown to the recipient, for example <c>PhotoSense Studio &lt;no-reply@mail.photosensestudio.com&gt;</c>.</param>
public sealed record EmailOptions(string ApiKey, string From)
{
    /// <summary>Environment variable that holds the Resend API key.</summary>
    public const string ApiKeyVariable = "RESEND_API_KEY";

    /// <summary>Environment variable that holds the sender.</summary>
    public const string FromVariable = "EMAIL_FROM";

    /// <summary>
    /// Reads the options from the process environment.
    /// </summary>
    /// <returns>The options, or <see langword="null"/> when no API key is set.</returns>
    /// <exception cref="InvalidOperationException">When the API key is set but the sender is not.</exception>
    public static EmailOptions? FromEnvironment()
    {
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var from = Environment.GetEnvironmentVariable(FromVariable);
        if (string.IsNullOrWhiteSpace(from))
        {
            throw new InvalidOperationException($"'{FromVariable}' must be set when '{ApiKeyVariable}' is set.");
        }

        return new EmailOptions(apiKey.Trim(), from.Trim());
    }
}

/// <summary>
/// Sends email through the Resend HTTP API. A provider failure is reported as <see cref="EmailDeliveryException"/> without
/// the response body, which could echo the message.
/// </summary>
/// <param name="httpClient">HTTP client pointed at Resend.</param>
/// <param name="options">Provider settings.</param>
public sealed class ResendEmailSender(HttpClient httpClient, EmailOptions options) : IEmailSender
{
    /// <summary>
    /// Address of the Resend API.
    /// </summary>
    public const string BaseAddress = "https://api.resend.com/";

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = options.From,
                to = new[] { message.To },
                subject = message.Subject,
                text = message.TextBody,
                html = message.HtmlBody,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new EmailDeliveryException("The email provider could not be reached.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new EmailDeliveryException($"The email provider refused the message (HTTP {(int)response.StatusCode}).");
            }
        }
    }
}

/// <summary>
/// Development sender: writes the email to the log instead of sending it, so the flow can be tried without a provider. It
/// is only registered in Development, because it prints verification codes.
/// </summary>
/// <param name="logger">Logger.</param>
public sealed partial class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        LogEmail(logger, message.To, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Logs an email that was not sent.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="to">Recipient.</param>
    /// <param name="subject">Subject.</param>
    /// <param name="body">Plain-text body.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Email not sent (development). To: {To}. Subject: {Subject}.\n{Body}")]
    private static partial void LogEmail(ILogger logger, string to, string subject, string body);
}

/// <summary>
/// Sender used when no provider is configured outside Development. It fails every send with a clear error instead of
/// dropping the email silently, so the API still starts and the endpoints that need email answer 503.
/// </summary>
public sealed class UnavailableEmailSender : IEmailSender
{
    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
        throw new EmailDeliveryException($"No email provider is configured; set '{EmailOptions.ApiKeyVariable}' and '{EmailOptions.FromVariable}'.");
}
