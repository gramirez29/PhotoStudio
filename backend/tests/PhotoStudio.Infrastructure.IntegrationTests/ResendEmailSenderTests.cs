using System.Net;
using System.Text.Json;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Infrastructure.Email;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of <see cref="ResendEmailSender"/> against a simulated Resend: no network is used.
/// </summary>
public sealed class ResendEmailSenderTests
{
    /// <summary>
    /// A simulated HTTP handler that records the body of the request and answers with a status.
    /// </summary>
    /// <param name="status">Status to answer with.</param>
    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        /// <summary>Gets the body of the last request.</summary>
        public string Body { get; private set; } = string.Empty;

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent("{}") };
        }
    }

    /// <summary>
    /// The images that travel inside the email are sent as attachments with their content id, in base64, which is how Resend shows
    /// them in the body.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task InlineImages_AreSentAsAttachmentsWithTheirContentId()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var sender = new ResendEmailSender(new HttpClient(handler) { BaseAddress = new Uri(ResendEmailSender.BaseAddress) }, new EmailOptions("key", "no-reply@mail.example.test"));

        await sender.SendAsync(
            new EmailMessage("c@example.com", "Asunto", "texto", "<img src=\"cid:logo\">", [new EmailImage("logo", "logo.png", "image/png", [1, 2, 3])]),
            TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(handler.Body);
        var attachment = body.RootElement.GetProperty("attachments").EnumerateArray().Single();
        attachment.GetProperty("content_id").GetString().ShouldBe("logo");
        attachment.GetProperty("filename").GetString().ShouldBe("logo.png");
        attachment.GetProperty("content_type").GetString().ShouldBe("image/png");
        attachment.GetProperty("content").GetString().ShouldBe(Convert.ToBase64String([1, 2, 3]));
        body.RootElement.GetProperty("html").GetString()!.ShouldContain("cid:logo");
    }

    /// <summary>
    /// A message without images sends no attachments.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task WithoutImages_NoAttachmentsAreSent()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var sender = new ResendEmailSender(new HttpClient(handler) { BaseAddress = new Uri(ResendEmailSender.BaseAddress) }, new EmailOptions("key", "no-reply@mail.example.test"));

        await sender.SendAsync(new EmailMessage("c@example.com", "Asunto", "texto"), TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(handler.Body);
        (body.RootElement.TryGetProperty("attachments", out var attachments) ? attachments.ValueKind : JsonValueKind.Null).ShouldBe(JsonValueKind.Null);
    }
}
