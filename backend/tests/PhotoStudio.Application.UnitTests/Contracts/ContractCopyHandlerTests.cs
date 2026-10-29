using Microsoft.Extensions.Logging.Abstractions;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Contracts;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Contracts;

/// <summary>
/// Tests of <see cref="ContractCopyHandler"/> and of the email it sends.
/// </summary>
public sealed class ContractCopyHandlerTests
{
    private const string SignedText = "CONTRATO DE SERVICIOS\nEntre PhotoSense Studio y María Pérez, cédula 112345678.";

    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly Guid _bookingId = Guid.CreateVersion7();
    private readonly Booking _booking;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContractCopyHandlerTests"/> class.
    /// </summary>
    public ContractCopyHandlerTests() => _booking = NewBooking("maria@example.com", _bookingId);

    /// <summary>
    /// A contract signed through the portal is emailed to the client, and the shipment is recorded so it is not sent again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_SendsTheSignedTextToTheClientAndRecordsIt()
    {
        GivenTheBookingIsStored();

        await NewHandler().HandleAsync(Signed(), TestContext.Current.CancellationToken);

        var message = _email.ReceivedCalls().Select(call => (EmailMessage)call.GetArguments()[0]!).ShouldHaveSingleItem();
        message.To.ShouldBe("maria@example.com");
        message.Subject.ShouldBe("Tu contrato firmado con PhotoSense Studio");
        message.TextBody.ShouldContain("Hola María Pérez,");
        message.TextBody.ShouldContain(SignedText);
        _booking.Contract!.CopySentAt.ShouldBe(BookingFactory.CreatedAt);
        await _bookings.Received(1).UpdateAsync(_booking, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The HTML body encodes the text, so a name with markup characters cannot break the email or inject anything.
    /// </summary>
    [Fact]
    public void SignedCopy_EncodesTheTextInTheHtmlBody()
    {
        var message = ContractEmails.SignedCopy("maria@example.com", "Ana <b>Lopez</b>", "Entre A & B <script>");

        message.HtmlBody.ShouldNotBeNull();
        message.HtmlBody.ShouldContain("&lt;script&gt;");
        message.HtmlBody.ShouldContain("A &amp; B");
        message.HtmlBody.ShouldNotContain("<script>");
        message.HtmlBody.ShouldContain("Ana &lt;b&gt;Lopez&lt;/b&gt;");
        message.HtmlBody.ShouldContain("white-space:pre-wrap");
    }

    /// <summary>
    /// A repeated delivery of the same event does not send the copy twice.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenDeliveredTwice_SendsOnlyOnce()
    {
        GivenTheBookingIsStored();
        var handler = NewHandler();

        await handler.HandleAsync(Signed(), TestContext.Current.CancellationToken);
        await handler.HandleAsync(Signed(), TestContext.Current.CancellationToken);

        await _email.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A contract the photographer registered in person or on paper has no text to send.
    /// </summary>
    /// <param name="channel">Channel of the signature.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData(Channel.InPerson)]
    [InlineData(Channel.External)]
    public async Task HandleAsync_ForAContractSignedByThePhotographer_SendsNothing(Channel channel)
    {
        GivenTheBookingIsStored();

        await NewHandler().HandleAsync(Signed() with { Channel = channel }, TestContext.Current.CancellationToken);

        await _email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await _bookings.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A booking without a client email has nowhere to send the copy.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithoutAClientEmail_SendsNothing()
    {
        var booking = NewBooking(email: null);
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        await NewHandler().HandleAsync(new ContractSigned(booking.Id, "v1", Channel.Portal, BookingFactory.CreatedAt), TestContext.Current.CancellationToken);

        await _email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An event for a booking that no longer exists is ignored.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_ForAMissingBooking_SendsNothing()
    {
        _bookings.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Booking?)null);

        await NewHandler().HandleAsync(Signed(), TestContext.Current.CancellationToken);

        await _email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When the provider does not accept the email the failure travels up, so the outbox retries later, and nothing is recorded.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheEmailFails_ThrowsAndRecordsNothing()
    {
        GivenTheBookingIsStored();
        _email.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new EmailDeliveryException("provider down")));

        await Should.ThrowAsync<EmailDeliveryException>(() => NewHandler().HandleAsync(Signed(), TestContext.Current.CancellationToken));

        _booking.Contract!.CopySentAt.ShouldBeNull();
        await _bookings.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When another write changes the booking while the shipment is recorded, the handler reads it again and records it, without
    /// sending the email a second time.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenRecordingConflicts_RetriesWithoutSendingAgain()
    {
        GivenEveryLoadReturnsAFreshBooking();
        var calls = 0;
        _bookings.UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>()).Returns(_ =>
            ++calls == 1 ? Task.FromException(new ConflictException(ApplicationErrorCodes.ConcurrencyConflict, "changed")) : Task.CompletedTask);

        await NewHandler().HandleAsync(Signed(), TestContext.Current.CancellationToken);

        await _email.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await _bookings.Received(2).UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// If recording keeps conflicting, the handler gives up without failing: retrying the event would only send the email again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenRecordingKeepsConflicting_DoesNotThrowOrResend()
    {
        GivenEveryLoadReturnsAFreshBooking();
        _bookings.UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ConflictException(ApplicationErrorCodes.ConcurrencyConflict, "changed")));

        await NewHandler().HandleAsync(Signed(), TestContext.Current.CancellationToken);

        await _email.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await _bookings.Received(3).UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Creates a booking the client signed through the portal, with the given email.
    /// </summary>
    /// <param name="email">Email of the client, or null.</param>
    /// <param name="id">Identifier of the booking; a new one when not given.</param>
    /// <returns>The booking.</returns>
    private static Booking NewBooking(string? email, Guid? id = null)
    {
        var booking = Booking.Create(
            id ?? Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            ClientContact.Create("María Pérez", "+506 8888-8888", email),
            "Retrato familiar",
            Money.Create(100_000m, "CRC"),
            TimeSlot.Create(BookingFactory.SessionStart, BookingFactory.SessionStart.AddHours(2)),
            BookingPolicy.Default,
            BookingFactory.CreatedAt);
        var declaration = ClientDeclaration.Create("112345678", 2, null, ImageUse.Authorized);
        booking.SignContract("María Pérez", "v1", Actor.Client, Channel.Portal, BookingFactory.CreatedAt, declaration, SignedText);
        return booking;
    }

    /// <summary>
    /// Makes the repository return the booking of the test.
    /// </summary>
    private void GivenTheBookingIsStored() =>
        _bookings.GetByIdAsync(_booking.Id, Arg.Any<CancellationToken>()).Returns(_booking);

    /// <summary>
    /// Makes every load return a new object of the same booking, as the database does, so a booking changed in memory by a failed
    /// write is not seen by the next attempt.
    /// </summary>
    private void GivenEveryLoadReturnsAFreshBooking() =>
        _bookings.GetByIdAsync(_bookingId, Arg.Any<CancellationToken>()).Returns(_ => NewBooking("maria@example.com", _bookingId));

    /// <summary>
    /// Builds the event of the booking of the test signed through the portal.
    /// </summary>
    /// <returns>The event.</returns>
    private ContractSigned Signed() => new(_booking.Id, "v1", Channel.Portal, BookingFactory.CreatedAt);

    /// <summary>
    /// Builds the handler under test.
    /// </summary>
    /// <returns>The handler.</returns>
    private ContractCopyHandler NewHandler() =>
        new(_bookings, _email, new FixedTimeProvider(BookingFactory.CreatedAt), NullLogger<ContractCopyHandler>.Instance);
}
