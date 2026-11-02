using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Emails;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Galleries;
using PhotoStudio.Application.Galleries.Proofs;
using PhotoStudio.Application.Portal;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;
using PhotoStudio.Domain.Galleries.Events;

namespace PhotoStudio.Application.UnitTests.Galleries;

/// <summary>
/// Tests of reopening the selection of a client and of the email that tells them.
/// </summary>
public sealed class SelectionReopenedTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly IGalleryRepository _galleries = Substitute.For<IGalleryRepository>();
    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly Booking _booking;
    private readonly Gallery _gallery;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelectionReopenedTests"/> class with a booking and a gallery being edited.
    /// </summary>
    public SelectionReopenedTests()
    {
        _booking = NewBooking("maria@example.com");
        var proofs = Enumerable.Range(0, 4).Select(index => new GalleryPhoto(Guid.CreateVersion7(), $"IMG_{index:0000}.jpg", 10, Now)).ToList();
        _gallery = Gallery.Restore(
            Guid.CreateVersion7(), 2, _booking.PhotographerId, _booking.Id, new GallerySnapshot("María Pérez Solano", "Retrato Familiar", Now.AddDays(-1)),
            GalleryStatus.InEditing, 2, 0, Money.Create(3_000m, "CRC"), Now, Now, proofs, null, proofs.Take(3).Select(photo => photo.Id), Now);
        _galleries.GetByIdAsync(_gallery.Id, Arg.Any<CancellationToken>()).Returns(_gallery);
        _bookings.GetByIdAsync(_booking.Id, Arg.Any<CancellationToken>()).Returns(_booking);
    }

    /// <summary>
    /// The handler reopens the gallery of the photographer, saves it and answers with it in selection and with the reason.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheHandler_ReopensAndSaves()
    {
        var response = await new ReopenSelectionHandler(_galleries, new FixedTimeProvider(Now))
            .HandleAsync(new ReopenSelectionCommand(_gallery.PhotographerId, _gallery.Id, "cambio de fotos"), TestContext.Current.CancellationToken);

        response.Status.ShouldBe(GalleryStatus.InSelection);
        response.SelectedPhotoCount.ShouldBe(0);
        response.SelectionReopenReason.ShouldBe("cambio de fotos");
        response.SelectionReopenedAt.ShouldBe(Now);
        await _galleries.Received(1).UpdateAsync(_gallery, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Without a reason, in another state, or for a gallery of another photographer, nothing is reopened or saved.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheHandler_RefusesWhatIsNotValid()
    {
        var handler = new ReopenSelectionHandler(_galleries, new FixedTimeProvider(Now));

        (await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(new ReopenSelectionCommand(_gallery.PhotographerId, _gallery.Id, null), TestContext.Current.CancellationToken)))
            .Code.ShouldBe(DomainErrorCodes.ReasonRequired);
        await Should.ThrowAsync<NotFoundException>(() => handler.HandleAsync(new ReopenSelectionCommand(Guid.CreateVersion7(), _gallery.Id, "x"), TestContext.Current.CancellationToken));
        await _galleries.DidNotReceive().UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The client is told once, without a link and without the reason, and a second delivery of the same event sends nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheClient_IsToldOnce_WithoutALinkOrTheReason()
    {
        _gallery.ReopenSelection("el cliente se arrepintió de dos fotos", Now);
        var occurred = new SelectionReopened(_gallery.Id, _booking.Id, _gallery.PhotographerId, Now);

        await Handler().HandleAsync(occurred, TestContext.Current.CancellationToken);
        await Handler().HandleAsync(occurred, TestContext.Current.CancellationToken);

        var message = _email.ReceivedCalls().Single().GetArguments()[0].ShouldBeOfType<EmailMessage>();
        message.To.ShouldBe("maria@example.com");
        message.Subject.ShouldBe("Tu selección está abierta otra vez");
        message.TextBody.ShouldContain("Hola María");
        message.TextBody.ShouldContain("tu paquete incluye 2");
        message.TextBody.ShouldContain("WhatsApp");
        message.TextBody.ShouldNotContain("http");
        message.TextBody.ShouldNotContain("arrepintió");
        message.HtmlBody!.ShouldNotContain("href=");
        _gallery.ReopenNotifiedAt.ShouldNotBeNull();
    }

    /// <summary>
    /// A later reopening is told again, even though the earlier one was already told.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ALaterReopening_IsToldAgain()
    {
        _gallery.MarkReopenNotified(Now.AddDays(-2), Now.AddDays(-2));
        _gallery.ReopenSelection("otra vez", Now);

        await Handler().HandleAsync(new SelectionReopened(_gallery.Id, _booking.Id, _gallery.PhotographerId, Now), TestContext.Current.CancellationToken);

        await _email.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A booking without an email, a booking that is gone and a gallery that is gone send nothing and are not errors.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task WithoutAnEmailToSendTo_NothingHappens()
    {
        _gallery.ReopenSelection("x", Now);
        var occurred = new SelectionReopened(_gallery.Id, _booking.Id, _gallery.PhotographerId, Now);
        _bookings.GetByIdAsync(_booking.Id, Arg.Any<CancellationToken>()).Returns(NewBooking(null));
        await Handler().HandleAsync(occurred, TestContext.Current.CancellationToken);
        _bookings.GetByIdAsync(_booking.Id, Arg.Any<CancellationToken>()).Returns((Booking?)null);
        await Handler().HandleAsync(occurred, TestContext.Current.CancellationToken);
        await Handler().HandleAsync(occurred with { GalleryId = Guid.CreateVersion7() }, TestContext.Current.CancellationToken);

        await _email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        _gallery.ReopenNotifiedAt.ShouldBeNull();
    }

    /// <summary>
    /// If the email provider refuses the message, nothing is recorded, so the outbox delivers the event again later.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task WhenTheEmailFails_NothingIsRecorded()
    {
        _gallery.ReopenSelection("x", Now);
        _email.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new EmailDeliveryException("down")));

        await Should.ThrowAsync<EmailDeliveryException>(() =>
            Handler().HandleAsync(new SelectionReopened(_gallery.Id, _booking.Id, _gallery.PhotographerId, Now), TestContext.Current.CancellationToken));

        _gallery.ReopenNotifiedAt.ShouldBeNull();
        await _galleries.DidNotReceive().UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Builds the handler that sends the email.
    /// </summary>
    /// <returns>The handler.</returns>
    private SelectionReopenedEmailHandler Handler() => new(_galleries, _bookings, _email, new FixedTimeProvider(Now));

    /// <summary>
    /// Builds a booking with an email, or without one.
    /// </summary>
    /// <param name="email">The email of the client.</param>
    /// <returns>The booking.</returns>
    private static Booking NewBooking(string? email) => Booking.Create(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        ClientContact.Create("María Pérez Solano", "+506 8888-8888", email),
        "Retrato Familiar",
        Money.Create(100_000m, "CRC"),
        TimeSlot.Create(Now.AddDays(10), Now.AddDays(10).AddHours(2)),
        BookingPolicy.Default,
        Now.AddDays(-5));
}
