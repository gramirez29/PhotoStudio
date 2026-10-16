using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Infrastructure.Push;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of <see cref="ExpoPushSender"/> against a simulated Expo service: no network is used.
/// </summary>
public sealed class ExpoPushSenderTests
{
    private static readonly IReadOnlyDictionary<string, string> Data = new Dictionary<string, string> { ["bookingId"] = "b1" };

    /// <summary>
    /// A simulated HTTP handler that records the requests and answers with what the test says.
    /// </summary>
    /// <param name="respond">Builds the answer for a request.</param>
    private sealed class StubHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        /// <summary>Gets the bodies of the requests received, in order.</summary>
        public List<string> Bodies { get; } = [];

        /// <summary>Gets the requests received, in order.</summary>
        public List<HttpRequestMessage> Requests { get; } = [];

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Bodies.Add(body);
            Requests.Add(request);
            return respond(request, body);
        }
    }

    /// <summary>
    /// Builds a sender over a simulated service.
    /// </summary>
    /// <param name="handler">Simulated service.</param>
    /// <param name="accessToken">Expo access token, or null.</param>
    /// <returns>The sender.</returns>
    private static ExpoPushSender NewSender(StubHandler handler, string? accessToken = null) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri(ExpoPushSender.BaseAddress) },
            new ExpoPushOptions(accessToken),
            NullLogger<ExpoPushSender>.Instance);

    /// <summary>
    /// Builds an answer made of tickets.
    /// </summary>
    /// <param name="tickets">JSON of each ticket.</param>
    /// <returns>The answer.</returns>
    private static HttpResponseMessage Tickets(params string[] tickets) =>
        new(HttpStatusCode.OK) { Content = new StringContent($"{{\"data\":[{string.Join(',', tickets)}]}}", Encoding.UTF8, "application/json") };

    /// <summary>
    /// Builds messages for numbered tokens.
    /// </summary>
    /// <param name="count">How many.</param>
    /// <returns>The messages.</returns>
    private static List<PushMessage> Messages(int count) =>
        [.. Enumerable.Range(1, count).Select(index => new PushMessage($"ExponentPushToken[t{index}]", "Title", "Body", Data))];

    /// <summary>
    /// The request carries the destination, the text, the data and the Android channel, and no authorization without a token.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SendAsync_PostsTheMessageInTheExpoShape()
    {
        var handler = new StubHandler((_, _) => Tickets("{\"status\":\"ok\",\"id\":\"1\"}"));

        var result = await NewSender(handler).SendAsync(Messages(1), TestContext.Current.CancellationToken);

        result.InvalidTokens.ShouldBeEmpty();
        handler.Requests.Single().RequestUri!.AbsoluteUri.ShouldBe("https://exp.host/--/api/v2/push/send");
        handler.Requests.Single().Headers.Authorization.ShouldBeNull();
        var message = JsonDocument.Parse(handler.Bodies.Single()).RootElement[0];
        message.GetProperty("to").GetString().ShouldBe("ExponentPushToken[t1]");
        message.GetProperty("title").GetString().ShouldBe("Title");
        message.GetProperty("body").GetString().ShouldBe("Body");
        message.GetProperty("data").GetProperty("bookingId").GetString().ShouldBe("b1");
        message.GetProperty("channelId").GetString().ShouldBe("default");
        message.GetProperty("priority").GetString().ShouldBe("high");
    }

    /// <summary>
    /// With an access token configured, the request is authorized with it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SendAsync_WithAnAccessToken_AuthorizesTheRequest()
    {
        var handler = new StubHandler((_, _) => Tickets("{\"status\":\"ok\"}"));

        await NewSender(handler, "secret").SendAsync(Messages(1), TestContext.Current.CancellationToken);

        handler.Requests.Single().Headers.Authorization!.ToString().ShouldBe("Bearer secret");
    }

    /// <summary>
    /// More messages than the service accepts per request are split into batches of 100.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SendAsync_SplitsLargeSendsIntoBatchesOfAHundred()
    {
        var handler = new StubHandler((_, body) => Tickets([.. Enumerable.Range(0, JsonDocument.Parse(body).RootElement.GetArrayLength()).Select(_ => "{\"status\":\"ok\"}")]));

        await NewSender(handler).SendAsync(Messages(250), TestContext.Current.CancellationToken);

        handler.Bodies.Select(body => JsonDocument.Parse(body).RootElement.GetArrayLength()).ShouldBe([100, 100, 50]);
    }

    /// <summary>
    /// A ticket that says the device is not registered reports its token, matched by position; other errors do not.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SendAsync_ReportsTheTokensOfUninstalledApps()
    {
        var handler = new StubHandler((_, _) => Tickets(
            "{\"status\":\"ok\"}",
            "{\"status\":\"error\",\"message\":\"gone\",\"details\":{\"error\":\"DeviceNotRegistered\"}}",
            "{\"status\":\"error\",\"message\":\"slow down\",\"details\":{\"error\":\"MessageRateExceeded\"}}",
            "{\"status\":\"error\",\"message\":\"no details\"}"));

        var result = await NewSender(handler).SendAsync(Messages(4), TestContext.Current.CancellationToken);

        result.InvalidTokens.ShouldBe(["ExponentPushToken[t2]"]);
    }

    /// <summary>
    /// An answer that is not a success, an unreachable service and an unexpected body are logged, not thrown, and the remaining
    /// batches are still sent.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SendAsync_WhenTheServiceFails_DoesNotThrowAndKeepsGoing()
    {
        var calls = 0;
        var handler = new StubHandler((_, _) => ++calls switch
        {
            1 => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            2 => throw new HttpRequestException("unreachable"),
            3 => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json") },
            4 => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"errors\":[]}") },
            _ => Tickets("{\"status\":\"error\",\"details\":{\"error\":\"DeviceNotRegistered\"}}"),
        });

        var result = await NewSender(handler).SendAsync(Messages(401), TestContext.Current.CancellationToken);

        handler.Bodies.Count.ShouldBe(5);
        result.InvalidTokens.ShouldBe(["ExponentPushToken[t401]"]);
    }

    /// <summary>
    /// A cancellation requested by the caller is not swallowed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SendAsync_WhenCancelled_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHandler((_, _) =>
        {
            cancellation.Cancel();
            throw new TaskCanceledException();
        });

        await Should.ThrowAsync<OperationCanceledException>(() => NewSender(handler).SendAsync(Messages(1), cancellation.Token));
    }

    /// <summary>
    /// Nothing to send makes no request.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SendAsync_WithoutMessages_MakesNoRequest()
    {
        var handler = new StubHandler((_, _) => Tickets());

        var result = await NewSender(handler).SendAsync([], TestContext.Current.CancellationToken);

        result.InvalidTokens.ShouldBeEmpty();
        handler.Requests.ShouldBeEmpty();
    }
}
