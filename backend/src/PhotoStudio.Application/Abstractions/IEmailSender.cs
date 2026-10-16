namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// An email ready to send.
/// </summary>
/// <param name="To">Recipient address.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="TextBody">Plain-text body; every email has one.</param>
/// <param name="HtmlBody">Optional HTML body.</param>
public sealed record EmailMessage(string To, string Subject, string TextBody, string? HtmlBody = null);

/// <summary>
/// Port that sends transactional email (verification codes, receipts). Implemented in the Infrastructure layer, so the
/// provider can change without touching the use cases.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends one email.
    /// </summary>
    /// <param name="message">Email to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the provider accepted the email.</returns>
    /// <exception cref="Exceptions.EmailDeliveryException">When the provider is not configured or did not accept the email.</exception>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
