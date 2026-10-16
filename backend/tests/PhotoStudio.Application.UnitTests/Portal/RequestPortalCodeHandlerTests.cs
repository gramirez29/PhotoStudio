using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Portal.RequestCode;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Portal;

namespace PhotoStudio.Application.UnitTests.Portal;

/// <summary>
/// Tests of <see cref="RequestPortalCodeHandler"/>.
/// </summary>
public sealed class RequestPortalCodeHandlerTests
{
    private readonly PortalFixture _fixture = new();

    /// <summary>
    /// The code goes to the email registered by the photographer, and only its hash is stored.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_SendsTheCodeToTheRegisteredEmailAndStoresOnlyItsHash()
    {
        var response = await NewHandler().HandleAsync(new RequestPortalCodeCommand(PortalFixture.Token), TestContext.Current.CancellationToken);

        response.MaskedEmail.ShouldBe("m***@example.com");
        response.ExpiresAt.ShouldBe(PortalFixture.Now + PortalOtp.Lifetime);
        await _fixture.Email.Received(1).SendAsync(
            Arg.Is<EmailMessage>(message => message.To == "maria@example.com" && message.TextBody.Contains("123456", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
        await _fixture.Codes.Received(1).SaveAsync(
            Arg.Is<PortalOtp>(otp => otp.CodeHash == "code-hash-123456" && otp.FailedAttempts == 0 && otp.SendCount == 1),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A token that opens nothing is a 404 and sends nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAnUnknownToken_ThrowsNotFoundAndSendsNothing()
    {
        await Should.ThrowAsync<NotFoundException>(() => NewHandler().HandleAsync(new RequestPortalCodeCommand("nope"), TestContext.Current.CancellationToken));

        await _fixture.Email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A revoked link, like an expired one, is reported as not found.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAnExpiredLink_ThrowsNotFound()
    {
        var expired = BookingLink.Issue(_fixture.Booking.Id, _fixture.Booking.PhotographerId, "hash-of-old", PortalFixture.Now.AddDays(-40), PortalFixture.Now.AddDays(-10));
        _fixture.Links.GetByTokenHashAsync("hash-of-old", Arg.Any<CancellationToken>()).Returns(expired);

        await Should.ThrowAsync<NotFoundException>(() => NewHandler().HandleAsync(new RequestPortalCodeCommand("old"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Without a client email there is nowhere to send the code, and the client cannot type another address.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithoutAClientEmail_ThrowsConflict()
    {
        var withoutEmail = Booking.Create(
            Guid.CreateVersion7(),
            _fixture.Booking.PhotographerId,
            ClientContact.Create("María Pérez", "+506 8888-8888"),
            "Retrato familiar",
            Money.Create(100_000m, "CRC"),
            TimeSlot.Create(PortalFixture.Now.AddDays(10), PortalFixture.Now.AddDays(10).AddHours(2)),
            BookingPolicy.Default,
            PortalFixture.Now);
        var link = BookingLink.Issue(withoutEmail.Id, withoutEmail.PhotographerId, "hash-of-bare", PortalFixture.Now, PortalFixture.Now.AddDays(30));
        _fixture.Links.GetByTokenHashAsync("hash-of-bare", Arg.Any<CancellationToken>()).Returns(link);
        _fixture.Bookings.GetByIdAsync(withoutEmail.Id, Arg.Any<CancellationToken>()).Returns(withoutEmail);

        var exception = await Should.ThrowAsync<ConflictException>(() => NewHandler().HandleAsync(new RequestPortalCodeCommand("bare"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.EmailMissing);
    }

    /// <summary>
    /// A second code asked for within the cooldown is refused with the time to wait.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithinTheCooldown_ThrowsRateLimited()
    {
        _fixture.Codes.GetAsync(_fixture.Booking.Id, Arg.Any<CancellationToken>()).Returns(_fixture.StoredCode(sentAgo: TimeSpan.FromSeconds(20)));

        var exception = await Should.ThrowAsync<OtpRateLimitedException>(() => NewHandler().HandleAsync(new RequestPortalCodeCommand(PortalFixture.Token), TestContext.Current.CancellationToken));

        exception.RetryAfter.ShouldBe(TimeSpan.FromSeconds(40));
        await _fixture.Email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// After the cooldown a new code is sent, counted in the same hourly window, and the failed guesses start again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_AfterTheCooldown_SendsANewCodeInTheSameWindow()
    {
        _fixture.Codes.GetAsync(_fixture.Booking.Id, Arg.Any<CancellationToken>()).Returns(_fixture.StoredCode(sentAgo: TimeSpan.FromMinutes(2), failedAttempts: 3, sendCount: 2));

        await NewHandler().HandleAsync(new RequestPortalCodeCommand(PortalFixture.Token), TestContext.Current.CancellationToken);

        await _fixture.Codes.Received(1).SaveAsync(
            Arg.Is<PortalOtp>(otp => otp.SendCount == 3 && otp.FailedAttempts == 0 && otp.WindowStartedAt == PortalFixture.Now.AddMinutes(-2)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Five codes in an hour is the cap: the sixth waits for the window to end.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_AtTheHourlyCap_ThrowsRateLimited()
    {
        _fixture.Codes.GetAsync(_fixture.Booking.Id, Arg.Any<CancellationToken>())
            .Returns(_fixture.StoredCode(sentAgo: TimeSpan.FromMinutes(5), sendCount: PortalOtp.MaxSendsPerWindow));

        var exception = await Should.ThrowAsync<OtpRateLimitedException>(() => NewHandler().HandleAsync(new RequestPortalCodeCommand(PortalFixture.Token), TestContext.Current.CancellationToken));

        exception.RetryAfter.ShouldBe(TimeSpan.FromMinutes(55));
    }

    /// <summary>
    /// When the provider fails the error reaches the caller, and no code was delivered.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheEmailFails_ThrowsEmailDeliveryFailed()
    {
        _fixture.Email.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new EmailDeliveryException("down")));

        await Should.ThrowAsync<EmailDeliveryException>(() => NewHandler().HandleAsync(new RequestPortalCodeCommand(PortalFixture.Token), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Builds the handler on the substituted ports.
    /// </summary>
    /// <returns>The handler.</returns>
    private RequestPortalCodeHandler NewHandler() =>
        new(_fixture.Bookings, _fixture.Links, _fixture.Codes, _fixture.CodeService, _fixture.Tokens, _fixture.Email, _fixture.Clock);
}
