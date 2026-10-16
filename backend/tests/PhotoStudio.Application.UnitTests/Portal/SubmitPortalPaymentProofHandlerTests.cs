using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Portal.SubmitProof;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Portal;

/// <summary>
/// Tests of <see cref="SubmitPortalPaymentProofHandler"/>.
/// </summary>
public sealed class SubmitPortalPaymentProofHandlerTests
{
    private readonly PortalFixture _fixture = new();

    /// <summary>
    /// A proof becomes a payment waiting for verification, with the reference, and is persisted.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_RegistersAPendingProofWithItsReference()
    {
        var response = await NewHandler().HandleAsync(Command(50_000m, "SINPE-998877", "key-1"), TestContext.Current.CancellationToken);

        var payment = _fixture.Booking.Payments.ShouldHaveSingleItem();
        payment.Status.ShouldBe(PaymentStatus.PendingVerification);
        payment.Channel.ShouldBe(Channel.Portal);
        payment.Reference.ShouldBe("SINPE-998877");
        response.Payments.ShouldHaveSingleItem().Status.ShouldBe(PaymentStatus.PendingVerification);
        response.Balance.Amount.ShouldBe(100_000m);
        await _fixture.Bookings.Received(1).UpdateAsync(_fixture.Booking, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Retrying with the same idempotency key registers one proof and writes once.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithARepeatedIdempotencyKey_DoesNotDuplicateTheProof()
    {
        await NewHandler().HandleAsync(Command(50_000m, null, "key-1"), TestContext.Current.CancellationToken);
        await NewHandler().HandleAsync(Command(50_000m, null, "key-1"), TestContext.Current.CancellationToken);

        _fixture.Booking.Payments.Count.ShouldBe(1);
        await _fixture.Bookings.Received(1).UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A session issued for another booking cannot act on this one, even with a valid link token.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithASessionOfAnotherBooking_ThrowsNotFound()
    {
        var command = Command(50_000m, null, "key-1") with { AuthorizedBookingId = Guid.CreateVersion7() };

        await Should.ThrowAsync<NotFoundException>(() => NewHandler().HandleAsync(command, TestContext.Current.CancellationToken));

        await _fixture.Bookings.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The client cannot declare more than what is owed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithMoreThanTheBalance_IsRejected()
    {
        var exception = await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(Command(100_001m, null, "key-1"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidAmount);
    }

    /// <summary>
    /// Proofs already waiting count against the balance, so several proofs cannot add up to more than is owed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_CountsProofsAlreadyWaiting()
    {
        await NewHandler().HandleAsync(Command(80_000m, null, "key-1"), TestContext.Current.CancellationToken);

        var exception = await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(Command(30_000m, null, "key-2"), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidAmount);
    }

    /// <summary>
    /// A zero amount is not a payment.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAZeroAmount_IsRejected()
    {
        await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(Command(0m, null, "key-1"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The idempotency key is required: without it a retry could not be recognized.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithoutAnIdempotencyKey_IsRejected()
    {
        var exception = await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(Command(10_000m, null, " "), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.RequiredValue);
    }

    /// <summary>
    /// A proof cannot be sent for a cancelled booking.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_ForACancelledBooking_IsRejected()
    {
        _fixture.Booking.Cancel(Actor.Photographer, null, PortalFixture.Now);

        await Should.ThrowAsync<DomainException>(() => NewHandler().HandleAsync(Command(10_000m, null, "key-1"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Builds a command for the booking of the fixture, as the session of that booking would send it.
    /// </summary>
    /// <param name="amount">Amount declared.</param>
    /// <param name="reference">Reference of the transfer.</param>
    /// <param name="key">Idempotency key.</param>
    /// <returns>The command.</returns>
    private SubmitPortalPaymentProofCommand Command(decimal amount, string? reference, string key) =>
        new(PortalFixture.Token, _fixture.Booking.Id, amount, reference, key);

    /// <summary>
    /// Builds the handler on the substituted ports.
    /// </summary>
    /// <returns>The handler.</returns>
    private SubmitPortalPaymentProofHandler NewHandler() =>
        new(_fixture.Bookings, _fixture.Links, _fixture.Users, _fixture.Tokens, _fixture.Clock);
}
