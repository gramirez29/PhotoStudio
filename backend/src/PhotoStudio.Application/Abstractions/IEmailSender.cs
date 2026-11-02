namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// An email ready to send.
/// </summary>
/// <param name="To">Recipient address.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="TextBody">Plain-text body; every email has one.</param>
/// <param name="InlineImages">Images that travel inside the email and are shown in its body, or <see langword="null"/> when there are none.</param>
/// <param name="HtmlBody">Optional HTML body.</param>
public sealed record EmailMessage(string To, string Subject, string TextBody, string? HtmlBody = null, IReadOnlyList<EmailImage>? InlineImages = null);

/// <summary>
/// An image that travels inside an email and is shown in its body, referenced from the HTML as <c>cid:{ContentId}</c>. Unlike a link to
/// an image on a website, it does not depend on the site being public or on the reader allowing remote images.
/// </summary>
/// <param name="ContentId">Identifier the HTML uses to point at the image.</param>
/// <param name="FileName">Name of the file.</param>
/// <param name="ContentType">Media type, for example <c>image/png</c>.</param>
/// <param name="Content">The bytes of the image.</param>
public sealed record EmailImage(string ContentId, string FileName, string ContentType, byte[] Content);

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
