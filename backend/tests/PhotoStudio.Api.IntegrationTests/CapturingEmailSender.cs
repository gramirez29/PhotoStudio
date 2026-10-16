using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// Email sender for the tests: keeps every message instead of sending it, so a test can read the verification code the client
/// would have received.
/// </summary>
public sealed partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    /// <summary>
    /// Gets the messages captured so far, oldest first.
    /// </summary>
    public IReadOnlyCollection<EmailMessage> Messages => _messages;

    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds the verification code in the latest email sent to an address.
    /// </summary>
    /// <param name="address">Recipient address.</param>
    /// <returns>The six-digit code.</returns>
    /// <exception cref="InvalidOperationException">When no email with a code was sent to the address.</exception>
    public string LastCodeFor(string address)
    {
        var message = _messages.LastOrDefault(candidate => string.Equals(candidate.To, address, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No email was sent to {address}.");
        var match = CodePattern().Match(message.TextBody);
        return match.Success ? match.Value : throw new InvalidOperationException("The email has no code.");
    }

    /// <summary>
    /// Matches a six-digit code that stands alone in the text.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"(?<!\d)\d{6}(?!\d)")]
    private static partial Regex CodePattern();
}
