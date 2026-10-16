using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Portal.VerifyCode;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Portal;

namespace PhotoStudio.Application.UnitTests.Portal;

/// <summary>
/// Tests of <see cref="VerifyPortalCodeHandler"/>.
/// </summary>
public sealed class VerifyPortalCodeHandlerTests
{
    private readonly PortalFixture _fixture = new();

    /// <summary>
    /// The right code opens a session for the booking and is spent.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithTheRightCode_IssuesASessionAndSpendsTheCode()
    {
        GivenAttemptReturns(_fixture.StoredCode(failedAttempts: 1));

        var session = await NewHandler().HandleAsync(new VerifyPortalCodeCommand(PortalFixture.Token, " 123456 "), TestContext.Current.CancellationToken);

        session.AccessToken.ShouldBe("portal-jwt");
        session.ExpiresAt.ShouldBe(PortalFixture.Now.AddMinutes(30));
        await _fixture.Codes.Received(1).DeleteAsync(_fixture.Booking.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A wrong code is rejected with the generic code and no session.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAWrongCode_IsRejected()
    {
        GivenAttemptReturns(_fixture.StoredCode(failedAttempts: 1));

        var exception = await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(new VerifyPortalCodeCommand(PortalFixture.Token, "654321"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidCode);
        _fixture.Sessions.DidNotReceive().Issue(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>());
        await _fixture.Codes.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The fifth wrong guess spends the code, so the right one no longer works afterwards.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_OnTheLastAllowedWrongGuess_SpendsTheCode()
    {
        GivenAttemptReturns(_fixture.StoredCode(failedAttempts: PortalOtp.MaxFailedAttempts));

        await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(new VerifyPortalCodeCommand(PortalFixture.Token, "654321"), TestContext.Current.CancellationToken));

        await _fixture.Codes.Received(1).DeleteAsync(_fixture.Booking.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Past the limit even the right code is refused: guesses counted beyond the limit never get compared.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_BeyondTheAttemptLimit_RefusesEvenTheRightCode()
    {
        GivenAttemptReturns(_fixture.StoredCode(failedAttempts: PortalOtp.MaxFailedAttempts + 1));

        var exception = await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(new VerifyPortalCodeCommand(PortalFixture.Token, "123456"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidCode);
        _fixture.Sessions.DidNotReceive().Issue(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>());
    }

    /// <summary>
    /// An expired code is refused, even if it matches.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAnExpiredCode_IsRejected()
    {
        GivenAttemptReturns(_fixture.StoredCode(sentAgo: PortalOtp.Lifetime + TimeSpan.FromSeconds(1)));

        var exception = await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(new VerifyPortalCodeCommand(PortalFixture.Token, "123456"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidCode);
    }

    /// <summary>
    /// With no code requested there is nothing to guess; the answer is the same generic failure.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithoutARequestedCode_IsRejected()
    {
        _fixture.Codes.RegisterAttemptAsync(_fixture.Booking.Id, Arg.Any<CancellationToken>()).Returns((PortalOtp?)null);

        var exception = await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(new VerifyPortalCodeCommand(PortalFixture.Token, "123456"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.InvalidCode);
    }

    /// <summary>
    /// Something that is not six digits is refused without counting an attempt, so garbage cannot burn the real code.
    /// </summary>
    /// <param name="typed">Value typed by the client.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    public async Task HandleAsync_WithAMalformedCode_IsRejectedWithoutCountingAnAttempt(string typed)
    {
        await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(new VerifyPortalCodeCommand(PortalFixture.Token, typed), TestContext.Current.CancellationToken));

        await _fixture.Codes.DidNotReceive().RegisterAttemptAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A token that opens nothing is a 404.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAnUnknownToken_ThrowsNotFound()
    {
        await Should.ThrowAsync<NotFoundException>(() => NewHandler().HandleAsync(new VerifyPortalCodeCommand("nope", "123456"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Makes the repository answer the guess with a record.
    /// </summary>
    /// <param name="record">Record as it is after counting the guess.</param>
    private void GivenAttemptReturns(PortalOtp record) =>
        _fixture.Codes.RegisterAttemptAsync(_fixture.Booking.Id, Arg.Any<CancellationToken>()).Returns(record);

    /// <summary>
    /// Builds the handler on the substituted ports.
    /// </summary>
    /// <returns>The handler.</returns>
    private VerifyPortalCodeHandler NewHandler() =>
        new(_fixture.Bookings, _fixture.Links, _fixture.Codes, _fixture.CodeService, _fixture.Tokens, _fixture.Sessions, _fixture.Clock);
}
