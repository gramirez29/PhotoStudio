using Microsoft.Extensions.Logging.Abstractions;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Notifications.Push;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.UnitTests.Notifications;

/// <summary>
/// Tests of <see cref="NotificationPusher"/> and of the handlers that register devices.
/// </summary>
public sealed class NotificationPusherTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 21, 0, 0, TimeSpan.Zero);

    private readonly IPushDeviceRepository _devices = Substitute.For<IPushDeviceRepository>();
    private readonly IPushSender _sender = Substitute.For<IPushSender>();
    private readonly NotificationPusher _pusher;

    /// <summary>
    /// Creates the fixture.
    /// </summary>
    public NotificationPusherTests()
    {
        _pusher = new NotificationPusher(_devices, _sender, NullLogger<NotificationPusher>.Instance);
        _sender.SendAsync(Arg.Any<IReadOnlyList<PushMessage>>(), Arg.Any<CancellationToken>()).Returns(new PushSendResult([]));
    }

    /// <summary>
    /// Builds a delivered proof notice for a photographer.
    /// </summary>
    /// <param name="photographerId">Photographer it is for.</param>
    /// <returns>The notification.</returns>
    private static Notification ProofNotice(Guid photographerId)
    {
        var notification = Notification.Schedule(
            photographerId, Guid.CreateVersion7(), NotificationType.PaymentProofSubmitted, Notification.PaymentProofKey(Guid.CreateVersion7()), Now, Now);
        notification.Deliver(
            new NotificationDetails("María Pérez", "+50688888888", "Retrato", Now, null, Money.Create(25_000m, "CRC")), Now);
        return notification;
    }

    /// <summary>
    /// Builds a device of a photographer.
    /// </summary>
    /// <param name="photographerId">Photographer it belongs to.</param>
    /// <param name="token">Its token.</param>
    /// <returns>The device.</returns>
    private static PushDevice Device(Guid photographerId, string token) => PushDevice.Register(token, photographerId, PushPlatform.Android, Now);

    /// <summary>
    /// Every phone of the photographer gets the push, with the text and the data the app needs to open the booking.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task PushAsync_SendsToEveryPhoneOfThePhotographer()
    {
        var photographerId = Guid.CreateVersion7();
        var notice = ProofNotice(photographerId);
        _devices.ListByPhotographerAsync(photographerId, Arg.Any<CancellationToken>())
            .Returns([Device(photographerId, "ExponentPushToken[a]"), Device(photographerId, "ExponentPushToken[b]")]);

        await _pusher.PushAsync([notice], TestContext.Current.CancellationToken);

        await _sender.Received(1).SendAsync(
            Arg.Is<IReadOnlyList<PushMessage>>(messages =>
                messages.Count == 2
                && messages.Select(message => message.Token).SequenceEqual(new[] { "ExponentPushToken[a]", "ExponentPushToken[b]" })
                && messages.All(message => message.Title == "Comprobante por verificar"
                    && message.Body == "María Pérez envió un comprobante de ₡25 000."
                    && message.Data["bookingId"] == notice.BookingId.ToString()
                    && message.Data["notificationId"] == notice.Id.ToString()
                    && message.Data["type"] == "PaymentProofSubmitted")),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A photographer without phones registered costs nothing: nothing is sent.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task PushAsync_WithoutPhones_SendsNothing()
    {
        var photographerId = Guid.CreateVersion7();
        _devices.ListByPhotographerAsync(photographerId, Arg.Any<CancellationToken>()).Returns([]);

        await _pusher.PushAsync([ProofNotice(photographerId)], TestContext.Current.CancellationToken);

        await _sender.DidNotReceiveWithAnyArgs().SendAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Each photographer only receives their own notices, even when one run delivers notices of several.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task PushAsync_KeepsEachPhotographerNoticesSeparate()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var firstNotice = ProofNotice(first);
        var secondNotice = ProofNotice(second);
        _devices.ListByPhotographerAsync(first, Arg.Any<CancellationToken>()).Returns([Device(first, "ExponentPushToken[first]")]);
        _devices.ListByPhotographerAsync(second, Arg.Any<CancellationToken>()).Returns([Device(second, "ExponentPushToken[second]")]);

        await _pusher.PushAsync([firstNotice, secondNotice], TestContext.Current.CancellationToken);

        await _sender.Received(1).SendAsync(
            Arg.Is<IReadOnlyList<PushMessage>>(messages =>
                messages.Count == 2
                && messages.Single(message => message.Token == "ExponentPushToken[first]").Data["bookingId"] == firstNotice.BookingId.ToString()
                && messages.Single(message => message.Token == "ExponentPushToken[second]").Data["bookingId"] == secondNotice.BookingId.ToString()),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A token the provider says is no longer valid is forgotten.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task PushAsync_ForgetsTheTokensTheProviderRejects()
    {
        var photographerId = Guid.CreateVersion7();
        _devices.ListByPhotographerAsync(photographerId, Arg.Any<CancellationToken>()).Returns([Device(photographerId, "ExponentPushToken[gone]")]);
        _sender.SendAsync(Arg.Any<IReadOnlyList<PushMessage>>(), Arg.Any<CancellationToken>()).Returns(new PushSendResult(["ExponentPushToken[gone]"]));

        await _pusher.PushAsync([ProofNotice(photographerId)], TestContext.Current.CancellationToken);

        await _devices.Received(1).DeleteTokensAsync(
            Arg.Is<IReadOnlyCollection<string>>(tokens => tokens.Single() == "ExponentPushToken[gone]"), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A failure while pushing never reaches the caller: the notice is already in the inbox.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task PushAsync_WhenSomethingFails_DoesNotThrow()
    {
        var photographerId = Guid.CreateVersion7();
        _devices.ListByPhotographerAsync(photographerId, Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<PushDevice>>(new InvalidOperationException("db down")));

        await Should.NotThrowAsync(() => _pusher.PushAsync([ProofNotice(photographerId)], TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A cancellation requested by the caller is not swallowed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task PushAsync_WhenCancelled_Propagates()
    {
        var photographerId = Guid.CreateVersion7();
        _devices.ListByPhotographerAsync(photographerId, Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<PushDevice>>(new OperationCanceledException()));

        await Should.ThrowAsync<OperationCanceledException>(() => _pusher.PushAsync([ProofNotice(photographerId)], TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Registering stores the device of the photographer with the current time.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RegisterHandler_StoresTheDevice()
    {
        var photographerId = Guid.CreateVersion7();
        var handler = new RegisterPushDeviceHandler(_devices, new FixedTimeProvider(Now));

        await handler.HandleAsync(new RegisterPushDeviceCommand(photographerId, "ExponentPushToken[abc]", PushPlatform.Android), TestContext.Current.CancellationToken);

        await _devices.Received(1).UpsertAsync(
            Arg.Is<PushDevice>(device => device.Token == "ExponentPushToken[abc]" && device.PhotographerId == photographerId && device.LastSeenAt == Now),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A token that is not an Expo push token is refused and nothing is stored.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RegisterHandler_WithAnInvalidToken_FailsAndStoresNothing()
    {
        var handler = new RegisterPushDeviceHandler(_devices, new FixedTimeProvider(Now));

        var exception = await Should.ThrowAsync<DomainException>(() =>
            handler.HandleAsync(new RegisterPushDeviceCommand(Guid.CreateVersion7(), "not-a-token", PushPlatform.Android), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidPushToken);
        await _devices.DidNotReceiveWithAnyArgs().UpsertAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Unregistering removes the device, scoped to the photographer.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UnregisterHandler_RemovesTheDeviceOfThePhotographer()
    {
        var photographerId = Guid.CreateVersion7();
        var handler = new UnregisterPushDeviceHandler(_devices);

        await handler.HandleAsync(new UnregisterPushDeviceCommand(photographerId, "ExponentPushToken[abc]"), TestContext.Current.CancellationToken);

        await _devices.Received(1).DeleteAsync(photographerId, "ExponentPushToken[abc]", Arg.Any<CancellationToken>());
    }
}
