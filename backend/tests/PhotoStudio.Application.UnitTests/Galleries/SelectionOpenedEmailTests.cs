using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Emails;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Galleries;
using PhotoStudio.Application.Portal;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;
using PhotoStudio.Domain.Galleries.Events;

namespace PhotoStudio.Application.UnitTests.Galleries;

/// <summary>
/// Tests of the email that tells the client the proofs are ready, and of the template of the branded emails.
/// </summary>
public sealed class SelectionOpenedEmailTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly IGalleryRepository _galleries = Substitute.For<IGalleryRepository>();
    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly Gallery _gallery;
    private readonly Booking _booking;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelectionOpenedEmailTests"/> class with a gallery in selection and its booking.
    /// </summary>
    public SelectionOpenedEmailTests()
    {
        _booking = NewBooking("maria@example.com");
        _gallery = Gallery.Restore(
            Guid.CreateVersion7(), 2, _booking.PhotographerId, _booking.Id, new GallerySnapshot("María Pérez Solano", "Retrato Familiar", Now.AddDays(-1)),
            GalleryStatus.InSelection, 30, 5, Money.Create(3_000m, "CRC"), Now, Now, [new GalleryPhoto(Guid.CreateVersion7(), "a.jpg", 10, Now)]);
        _galleries.GetByIdAsync(_gallery.Id, Arg.Any<CancellationToken>()).Returns(_gallery);
        _bookings.GetByIdAsync(_booking.Id, Arg.Any<CancellationToken>()).Returns(_booking);
    }

    /// <summary>
    /// The client gets one email with the facts: who it is for, how many photos the package gives and what an extra costs, that the
    /// proofs are low quality with a watermark, and the commitment to excellent quality. It has no link.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheClient_IsToldOnce_WithTheFacts_AndNoLink()
    {
        await Handler().HandleAsync(new SelectionOpened(_gallery.Id, _booking.Id, _gallery.PhotographerId, Now), TestContext.Current.CancellationToken);

        var message = _email.ReceivedCalls().Single().GetArguments()[0].ShouldBeOfType<EmailMessage>();
        message.To.ShouldBe("maria@example.com");
        message.Subject.ShouldBe("Tus fotos de prueba ya están listas");
        message.TextBody.ShouldContain("Hola María");
        message.TextBody.ShouldNotContain("Pérez Solano");
        message.TextBody.ShouldContain("tu paquete incluye 35");
        message.TextBody.ShouldContain("₡3");
        message.TextBody.ShouldContain("calidad baja y con marca de agua");
        message.TextBody.ShouldContain("excelente calidad");
        message.TextBody.ShouldContain("WhatsApp");
        message.TextBody.ShouldNotContain("http");
        message.HtmlBody!.ShouldNotContain("href=");
        _gallery.SelectionNotifiedAt.ShouldBe(Now);
        await _galleries.Received(1).UpdateAsync(_gallery, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A second delivery of the same event sends nothing: the gallery remembers that the client was told.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ASecondDelivery_SendsNothing()
    {
        _gallery.MarkClientNotified(Now.AddMinutes(-1));

        await Handler().HandleAsync(new SelectionOpened(_gallery.Id, _booking.Id, _gallery.PhotographerId, Now), TestContext.Current.CancellationToken);

        await _email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await _galleries.DidNotReceive().UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A booking without an email, a booking that is gone and a gallery that is gone send nothing and are not errors, so the outbox
    /// does not retry them forever.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task WithoutAnEmailToSendTo_NothingHappens()
    {
        _bookings.GetByIdAsync(_booking.Id, Arg.Any<CancellationToken>()).Returns(NewBooking(null));
        await Handler().HandleAsync(new SelectionOpened(_gallery.Id, _booking.Id, _gallery.PhotographerId, Now), TestContext.Current.CancellationToken);
        _bookings.GetByIdAsync(_booking.Id, Arg.Any<CancellationToken>()).Returns((Booking?)null);
        await Handler().HandleAsync(new SelectionOpened(_gallery.Id, _booking.Id, _gallery.PhotographerId, Now), TestContext.Current.CancellationToken);
        await Handler().HandleAsync(new SelectionOpened(Guid.CreateVersion7(), _booking.Id, _gallery.PhotographerId, Now), TestContext.Current.CancellationToken);

        await _email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        _gallery.SelectionNotifiedAt.ShouldBeNull();
    }

    /// <summary>
    /// If the email provider refuses the message, nothing is recorded, so the outbox delivers the event again later.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task WhenTheEmailFails_NothingIsRecorded_AndTheErrorGoesToTheOutbox()
    {
        _email.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new EmailDeliveryException("down")));

        await Should.ThrowAsync<EmailDeliveryException>(() =>
            Handler().HandleAsync(new SelectionOpened(_gallery.Id, _booking.Id, _gallery.PhotographerId, Now), TestContext.Current.CancellationToken));

        _gallery.SelectionNotifiedAt.ShouldBeNull();
        await _galleries.DidNotReceive().UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When recording that the client was told collides with another change, the gallery is reloaded and the record is written again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task WhenRecordingCollides_ItIsWrittenAgainOnTheFreshGallery()
    {
        var fresh = Gallery.Restore(_gallery.Id, 3, _gallery.PhotographerId, _gallery.BookingId, _gallery.Snapshot, GalleryStatus.InSelection, 30, 5, _gallery.ExtraPhotoPrice, Now, Now);
        _galleries.GetByIdAsync(_gallery.Id, Arg.Any<CancellationToken>()).Returns(_gallery, fresh);
        _galleries.UpdateAsync(_gallery, Arg.Any<CancellationToken>()).Returns(Task.FromException(new ConflictException(ApplicationErrorCodes.ConcurrencyConflict, "x")));

        await Handler().HandleAsync(new SelectionOpened(_gallery.Id, _booking.Id, _gallery.PhotographerId, Now), TestContext.Current.CancellationToken);

        fresh.SelectionNotifiedAt.ShouldBe(Now);
        await _galleries.Received(1).UpdateAsync(fresh, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The branded email has the look of the brand: the slate blue band with the name, the cream page, the peach card, the terracotta
    /// rule and the dark palette for mail apps in dark mode; a table layout; and the logo only when it is given.
    /// </summary>
    [Fact]
    public void TheBrandedEmail_HasTheLookOfTheBrand()
    {
        var content = new EmailContent("Pre", "Título", ["Un párrafo."], ["Paso uno", "Paso dos"], "Una nota.", "Cierre.");

        var html = BrandedEmail.RenderHtml("PhotoSense Studio", "https://site.test/icon-192.png", content);
        var withoutLogo = BrandedEmail.RenderHtml("PhotoSense Studio", null, content);

        html.ShouldContain("lang=\"es\"");
        html.ShouldContain("#3A4466");
        html.ShouldContain("#FBF3EA");
        html.ShouldContain("#F6D5BE");
        html.ShouldContain("#C4552E");
        html.ShouldContain("#020D2C");
        html.ShouldContain("#E3A97F");
        html.ShouldContain("prefers-color-scheme: dark");
        html.ShouldContain("role=\"presentation\"");
        html.ShouldContain("<img src=\"https://site.test/icon-192.png\"");
        html.ShouldContain("PhotoSense Studio");
        withoutLogo.ShouldNotContain("<img");
        html.ShouldNotContain("<script");
        html.ShouldNotContain("href=");
    }

    /// <summary>
    /// Anything that comes from data is encoded, so a name with markup cannot inject any into the email.
    /// </summary>
    [Fact]
    public void TheBrandedEmail_EncodesWhatComesFromData()
    {
        var content = new EmailContent("<b>pre</b>", "<script>alert(1)</script>", ["Hola <img src=x onerror=alert(1)>"], ["<i>paso</i>"], "<u>nota</u>", "<a href=evil>cierre</a>");

        var html = BrandedEmail.RenderHtml("Studio <x>", "https://site.test/a.png?x=\"1\"", content);

        html.ShouldNotContain("<script>");
        html.ShouldNotContain("<img src=x");
        html.ShouldNotContain("<i>paso");
        html.ShouldNotContain("<a href=evil>");
        html.ShouldNotContain("<x>");
        html.ShouldContain("&lt;script&gt;");
        html.ShouldContain("&lt;img src=x onerror=alert(1)&gt;");
        html.ShouldContain("&quot;");
    }

    /// <summary>
    /// The text version says the same as the HTML one: heading, paragraphs, numbered steps, note and closing, and the signature.
    /// </summary>
    [Fact]
    public void TheTextVersion_SaysTheSame()
    {
        var content = new EmailContent("Pre", "Título", ["Un párrafo."], ["Paso uno", "Paso dos"], "Una nota.", "Cierre.");

        var text = BrandedEmail.RenderText("PhotoSense Studio", content);

        text.ShouldBe("Título\n\nUn párrafo.\n\n1. Paso uno\n2. Paso dos\n\nUna nota.\n\nCierre.\n\nPhotoSense Studio");
    }

    /// <summary>
    /// Builds the handler that sends the email.
    /// </summary>
    /// <returns>The handler.</returns>
    private SelectionOpenedEmailHandler Handler() => new(_galleries, _bookings, _email, new PortalSettings("https://develop.photosensestudio.com"), new FixedTimeProvider(Now));

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
