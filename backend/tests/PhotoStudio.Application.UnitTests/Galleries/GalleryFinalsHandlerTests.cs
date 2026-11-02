using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Emails;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Galleries;
using PhotoStudio.Application.Galleries.Finals;
using PhotoStudio.Application.Portal;
using PhotoStudio.Application.Storage;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;
using PhotoStudio.Domain.Galleries.Events;

namespace PhotoStudio.Application.UnitTests.Galleries;

/// <summary>
/// Tests of the final photos: the places to upload them, registering what was really uploaded, publishing, the delivery when the
/// balance is zero, the email about it and the extras released by hand.
/// </summary>
public sealed class GalleryFinalsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly IGalleryRepository _galleries = Substitute.For<IGalleryRepository>();
    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly IObjectStorage _storage = Substitute.For<IObjectStorage>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly Booking _booking;
    private Gallery _gallery;

    /// <summary>
    /// Initializes a new instance of the <see cref="GalleryFinalsHandlerTests"/> class with a booking and a gallery being edited.
    /// </summary>
    public GalleryFinalsHandlerTests()
    {
        _booking = NewBooking("maria@example.com");
        _gallery = NewGallery(GalleryStatus.InEditing, photos: 5, included: 2, chosen: 3);
        _bookings.GetByIdAsync(_booking.Id, Arg.Any<CancellationToken>()).Returns(_booking);
        _storage.CreateUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(call => new PresignedUrl(new Uri($"https://storage.test/{call.ArgAt<string>(0)}?sig=x"), Now.AddMinutes(15), new Dictionary<string, string> { ["Content-Type"] = "image/jpeg" }));
    }

    /// <summary>
    /// The photographer gets one place per final, for photos the client chose, under the key of the final.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RequestUploads_GivesAPlacePerChosenPhoto()
    {
        var chosen = _gallery.SelectedPhotoIds.Take(2).ToList();

        var response = await new RequestFinalUploadsHandler(_galleries, _storage).HandleAsync(
            new RequestFinalUploadsCommand(_gallery.PhotographerId, _gallery.Id, [.. chosen.Select(id => new FinalFile(id, 5_000_000))]), TestContext.Current.CancellationToken);

        response.Slots.Select(slot => slot.PhotoId).ShouldBe(chosen);
        response.Slots[0].Url.ToString().ShouldContain($"galleries/{_gallery.Id:D}/finals/{chosen[0]:D}.jpg");
    }

    /// <summary>
    /// A photo the client did not choose, a size that is too big, and a gallery of another photographer are all refused.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RequestUploads_RefusesWhatIsNotValid()
    {
        var handler = new RequestFinalUploadsHandler(_galleries, _storage);
        var notChosen = _gallery.Photos.First(photo => !_gallery.SelectedPhotoIds.Contains(photo.Id)).Id;

        var foreign = await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(
            new RequestFinalUploadsCommand(_gallery.PhotographerId, _gallery.Id, [new FinalFile(notChosen, 100)]), TestContext.Current.CancellationToken));
        var big = await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(
            new RequestFinalUploadsCommand(_gallery.PhotographerId, _gallery.Id, [new FinalFile(_gallery.SelectedPhotoIds[0], GalleryPhoto.MaxFinalBytes + 1)]), TestContext.Current.CancellationToken));

        foreign.Code.ShouldBe(DomainErrorCodes.GalleryInvalidPhoto);
        big.Code.ShouldBe(DomainErrorCodes.GalleryInvalidPhoto);
        await Should.ThrowAsync<NotFoundException>(() => handler.HandleAsync(
            new RequestFinalUploadsCommand(Guid.CreateVersion7(), _gallery.Id, [new FinalFile(_gallery.SelectedPhotoIds[0], 100)]), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// After publishing, a place is given only for the extras: the finals of the package are frozen.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RequestUploads_AfterPublishing_OnlyForTheExtras()
    {
        _gallery = NewGallery(GalleryStatus.Ready, photos: 5, included: 2, chosen: 3, finalsOfThePackage: true);
        var handler = new RequestFinalUploadsHandler(_galleries, _storage);

        var package = await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(
            new RequestFinalUploadsCommand(_gallery.PhotographerId, _gallery.Id, [new FinalFile(_gallery.PackagePhotoIds[0], 100)]), TestContext.Current.CancellationToken));
        var extra = await handler.HandleAsync(
            new RequestFinalUploadsCommand(_gallery.PhotographerId, _gallery.Id, [new FinalFile(_gallery.ExtraPhotoIds[0], 100)]), TestContext.Current.CancellationToken);

        package.Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
        extra.Slots.Count.ShouldBe(1);
    }

    /// <summary>
    /// Registering looks at the real object: a JPEG within the limit is kept, and what is missing, of another type, empty or too big
    /// is reported with its reason and deleted from the storage.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_KeepsTheValid_AndReportsAndDeletesTheRest()
    {
        var ids = _gallery.SelectedPhotoIds;
        Stored(ids[0], new StoredObjectInfo(5_000_000, "image/jpeg"));
        Stored(ids[1], new StoredObjectInfo(100, "text/html"));
        Stored(ids[2], new StoredObjectInfo(GalleryPhoto.MaxFinalBytes + 1, "image/jpeg"));

        var response = await new RegisterFinalsHandler(_galleries, _storage, new FixedTimeProvider(Now))
            .HandleAsync(new RegisterFinalsCommand(_gallery.PhotographerId, _gallery.Id, [.. ids, Guid.CreateVersion7()]), TestContext.Current.CancellationToken);

        _gallery.HasFinal(ids[0]).ShouldBeTrue();
        response.Rejected.Select(rejection => rejection.Reason).ShouldBe(["wrong_type", "too_big", "missing"]);
        await _storage.Received(1).DeleteAsync(GalleryKeys.Final(_gallery.Id, ids[1]), Arg.Any<CancellationToken>());
        await _storage.Received(1).DeleteAsync(GalleryKeys.Final(_gallery.Id, ids[2]), Arg.Any<CancellationToken>());
        await _galleries.Received(1).UpdateAsync(_gallery, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When a replacement is refused, the final that was registered for that photo is forgotten too, because the object it pointed to
    /// was overwritten by the bad file and deleted.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WhenAReplacementIsRefused_TheOldFinalIsForgotten()
    {
        var photoId = _gallery.SelectedPhotoIds[0];
        _gallery.RegisterFinal(photoId, 1_000, Now);
        Stored(photoId, new StoredObjectInfo(100, "text/html"));

        await new RegisterFinalsHandler(_galleries, _storage, new FixedTimeProvider(Now))
            .HandleAsync(new RegisterFinalsCommand(_gallery.PhotographerId, _gallery.Id, [photoId]), TestContext.Current.CancellationToken);

        _gallery.HasFinal(photoId).ShouldBeFalse();
    }

    /// <summary>
    /// Two batches registered at the same time may collide; the handler reloads the gallery and tries again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WhenTheGalleryChangedMeanwhile_TriesAgain()
    {
        var ids = _gallery.SelectedPhotoIds;
        Stored(ids[0], new StoredObjectInfo(1_000, "image/jpeg"));
        var fresh = Gallery.Restore(
            _gallery.Id, 3, _gallery.PhotographerId, _gallery.BookingId, _gallery.Snapshot, GalleryStatus.InEditing, 2, 0, _gallery.ExtraPhotoPrice, Now, Now,
            _gallery.Photos, null, _gallery.SelectedPhotoIds, Now);
        _galleries.GetByIdAsync(_gallery.Id, Arg.Any<CancellationToken>()).Returns(_gallery, fresh);
        _galleries.UpdateAsync(_gallery, Arg.Any<CancellationToken>()).Returns(Task.FromException(new ConflictException(ApplicationErrorCodes.ConcurrencyConflict, "x")));

        await new RegisterFinalsHandler(_galleries, _storage, new FixedTimeProvider(Now))
            .HandleAsync(new RegisterFinalsCommand(_gallery.PhotographerId, _gallery.Id, [ids[0]]), TestContext.Current.CancellationToken);

        fresh.HasFinal(ids[0]).ShouldBeTrue();
        await _galleries.Received(1).UpdateAsync(fresh, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Publishing saves the gallery as ready, and refuses while a photo of the package has no final.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Publish_NeedsThePackageComplete()
    {
        var handler = new PublishFinalsHandler(_galleries, new FixedTimeProvider(Now));
        var incomplete = await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(new PublishFinalsCommand(_gallery.PhotographerId, _gallery.Id), TestContext.Current.CancellationToken));
        incomplete.Code.ShouldBe(DomainErrorCodes.GalleryFinalsIncomplete);

        foreach (var photoId in _gallery.PackagePhotoIds)
        {
            _gallery.RegisterFinal(photoId, 100, Now);
        }

        var response = await handler.HandleAsync(new PublishFinalsCommand(_gallery.PhotographerId, _gallery.Id), TestContext.Current.CancellationToken);

        response.Status.ShouldBe(GalleryStatus.Ready);
        await _galleries.Received(1).UpdateAsync(_gallery, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// With the balance at zero, publishing delivers the gallery in the same pass.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Delivery_WhenTheBalanceIsZero_DeliversAfterPublishing()
    {
        _gallery = NewGallery(GalleryStatus.Ready, photos: 4, included: 2, chosen: 2, finalsOfThePackage: true);
        _booking.RecordInPersonPayment(Guid.CreateVersion7(), _booking.PackagePrice, PaymentMethod.Cash, "paid", Now);

        await new FinalPhotosReadyDeliveryHandler(_galleries, Delivery())
            .HandleAsync(new FinalPhotosReady(_gallery.Id, _booking.Id, _gallery.PhotographerId, 2, Now), TestContext.Current.CancellationToken);

        _gallery.Status.ShouldBe(GalleryStatus.Delivered);
        await _galleries.Received(1).UpdateAsync(_gallery, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// With money still owed the gallery stays ready: the client cannot download without paying.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Delivery_WhenThereIsABalance_DoesNothing()
    {
        _gallery = NewGallery(GalleryStatus.Ready, photos: 4, included: 2, chosen: 2, finalsOfThePackage: true);
        _booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(1_000m, "CRC"), PaymentMethod.Cash, "part", Now);

        await new FinalPhotosReadyDeliveryHandler(_galleries, Delivery())
            .HandleAsync(new FinalPhotosReady(_gallery.Id, _booking.Id, _gallery.PhotographerId, 2, Now), TestContext.Current.CancellationToken);

        _gallery.Status.ShouldBe(GalleryStatus.Ready);
        await _galleries.DidNotReceive().UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The payment that clears the balance, even weeks after publishing, delivers the gallery; a booking with no gallery, or a gallery
    /// that is not ready, does nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Delivery_WhenThePaymentClearsTheBalance_Delivers()
    {
        _gallery = NewGallery(GalleryStatus.Ready, photos: 4, included: 2, chosen: 2, finalsOfThePackage: true);
        _booking.RecordInPersonPayment(Guid.CreateVersion7(), _booking.PackagePrice, PaymentMethod.SinpeMovil, "rest", Now);
        var handler = new PaymentVerifiedDeliveryHandler(_galleries, Delivery());
        var verified = new PaymentVerified(_booking.Id, Guid.CreateVersion7(), 100_000m, "CRC", PaymentMethod.SinpeMovil, Channel.InPerson, Now);

        await handler.HandleAsync(verified, TestContext.Current.CancellationToken);
        await handler.HandleAsync(verified with { BookingId = Guid.CreateVersion7() }, TestContext.Current.CancellationToken);

        _gallery.Status.ShouldBe(GalleryStatus.Delivered);
        await _galleries.Received(1).UpdateAsync(_gallery, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A gallery that is delivered already, or that is still being edited, is not delivered again nor early.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Delivery_OfAGalleryThatIsNotReady_DoesNothing()
    {
        _booking.RecordInPersonPayment(Guid.CreateVersion7(), _booking.PackagePrice, PaymentMethod.Cash, "paid", Now);
        var verified = new PaymentVerified(_booking.Id, Guid.CreateVersion7(), 100_000m, "CRC", PaymentMethod.Cash, Channel.InPerson, Now);

        await new PaymentVerifiedDeliveryHandler(_galleries, Delivery()).HandleAsync(verified, TestContext.Current.CancellationToken);

        _gallery.Status.ShouldBe(GalleryStatus.InEditing);
        await _galleries.DidNotReceive().UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// If the delivery collides with another change, the gallery is reloaded and delivered on the fresh copy.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Delivery_WhenSavingCollides_TriesOnTheFreshGallery()
    {
        _gallery = NewGallery(GalleryStatus.Ready, photos: 4, included: 2, chosen: 2, finalsOfThePackage: true);
        var stale = _gallery;
        var fresh = NewGallery(GalleryStatus.Ready, photos: 4, included: 2, chosen: 2, finalsOfThePackage: true, id: stale.Id);
        _booking.RecordInPersonPayment(Guid.CreateVersion7(), _booking.PackagePrice, PaymentMethod.Cash, "paid", Now);
        _galleries.GetByIdAsync(stale.Id, Arg.Any<CancellationToken>()).Returns(stale, fresh);
        _galleries.UpdateAsync(stale, Arg.Any<CancellationToken>()).Returns(Task.FromException(new ConflictException(ApplicationErrorCodes.ConcurrencyConflict, "x")));

        await new FinalPhotosReadyDeliveryHandler(_galleries, Delivery())
            .HandleAsync(new FinalPhotosReady(stale.Id, _booking.Id, stale.PhotographerId, 2, Now), TestContext.Current.CancellationToken);

        fresh.Status.ShouldBe(GalleryStatus.Delivered);
    }

    /// <summary>
    /// The client is told once that the photos are ready, without a link, and a second delivery of the event sends nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task DeliveredEmail_IsSentOnce_AndHasNoLink()
    {
        _gallery = NewGallery(GalleryStatus.Delivered, photos: 3, included: 2, chosen: 2, finalsOfThePackage: true);
        var handler = new GalleryDeliveredEmailHandler(_galleries, _bookings, _email, new PortalSettings("https://develop.photosensestudio.com"), new FixedTimeProvider(Now));
        var delivered = new GalleryDelivered(_gallery.Id, _booking.Id, _gallery.PhotographerId, Now);

        await handler.HandleAsync(delivered, TestContext.Current.CancellationToken);
        await handler.HandleAsync(delivered, TestContext.Current.CancellationToken);

        var message = _email.ReceivedCalls().Single().GetArguments()[0].ShouldBeOfType<EmailMessage>();
        message.To.ShouldBe("maria@example.com");
        message.Subject.ShouldBe("Tus fotos ya están listas");
        message.TextBody.ShouldContain("Hola María");
        message.TextBody.ShouldContain("WhatsApp");
        message.TextBody.ShouldNotContain("http");
        message.HtmlBody!.ShouldNotContain("href=");
        _gallery.DeliveryNotifiedAt.ShouldBe(Now);
    }

    /// <summary>
    /// A booking without an email sends nothing and is not an error.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task DeliveredEmail_WithoutAnAddress_SendsNothing()
    {
        _gallery = NewGallery(GalleryStatus.Delivered, photos: 3, included: 2, chosen: 2, finalsOfThePackage: true);
        _bookings.GetByIdAsync(_booking.Id, Arg.Any<CancellationToken>()).Returns(NewBooking(null));

        await new GalleryDeliveredEmailHandler(_galleries, _bookings, _email, new PortalSettings("https://x.test"), new FixedTimeProvider(Now))
            .HandleAsync(new GalleryDelivered(_gallery.Id, _booking.Id, _gallery.PhotographerId, Now), TestContext.Current.CancellationToken);

        await _email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Releasing the extras is saved once and needs a final of an extra; releasing again saves nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ReleaseExtras_IsSavedOnce()
    {
        _gallery = NewGallery(GalleryStatus.Delivered, photos: 5, included: 2, chosen: 3, finalsOfThePackage: true);
        var handler = new ReleaseExtrasHandler(_galleries, new FixedTimeProvider(Now));
        var none = await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(new ReleaseExtrasCommand(_gallery.PhotographerId, _gallery.Id), TestContext.Current.CancellationToken));
        none.Code.ShouldBe(DomainErrorCodes.GalleryFinalsIncomplete);

        _gallery.RegisterFinal(_gallery.ExtraPhotoIds[0], 100, Now);
        await handler.HandleAsync(new ReleaseExtrasCommand(_gallery.PhotographerId, _gallery.Id), TestContext.Current.CancellationToken);
        await handler.HandleAsync(new ReleaseExtrasCommand(_gallery.PhotographerId, _gallery.Id), TestContext.Current.CancellationToken);

        _gallery.ExtrasReleasedAt.ShouldBe(Now);
        await _galleries.Received(1).UpdateAsync(_gallery, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Builds the service that delivers galleries.
    /// </summary>
    /// <returns>The service.</returns>
    private GalleryDelivery Delivery() => new(_galleries, _bookings, new FixedTimeProvider(Now));

    /// <summary>
    /// Tells the storage what it holds for the final of a photo.
    /// </summary>
    /// <param name="photoId">Photo the final belongs to.</param>
    /// <param name="info">What the storage reports.</param>
    private void Stored(Guid photoId, StoredObjectInfo info) =>
        _storage.GetInfoAsync(GalleryKeys.Final(_gallery.Id, photoId), Arg.Any<CancellationToken>()).Returns(info);

    /// <summary>
    /// Builds a gallery of the booking, with its chosen photos, and makes the repository return it.
    /// </summary>
    /// <param name="status">State of the gallery.</param>
    /// <param name="photos">Proofs in the gallery.</param>
    /// <param name="included">Photos the package covers.</param>
    /// <param name="chosen">How many photos the client chose.</param>
    /// <param name="finalsOfThePackage">Whether the finals of the package are already uploaded.</param>
    /// <param name="id">Identifier, when it must be the same as another copy.</param>
    /// <returns>The gallery.</returns>
    private Gallery NewGallery(GalleryStatus status, int photos, int included, int chosen, bool finalsOfThePackage = false, Guid? id = null)
    {
        var proofs = Enumerable.Range(0, photos).Select(index => new GalleryPhoto(Guid.CreateVersion7(), $"IMG_{index:0000}.jpg", 10, Now)).ToList();
        var chosenIds = proofs.Take(chosen).Select(photo => photo.Id).ToList();
        if (finalsOfThePackage)
        {
            proofs = [.. proofs.Select(photo => chosenIds.Take(included).Contains(photo.Id) ? photo with { FinalBytes = 100, FinalUploadedAt = Now } : photo)];
        }

        var gallery = Gallery.Restore(
            id ?? Guid.CreateVersion7(), 2, _booking.PhotographerId, _booking.Id, new GallerySnapshot("María Pérez Solano", "Retrato Familiar", Now.AddDays(-1)),
            status, included, 0, Money.Create(3_000m, "CRC"), Now, Now, proofs, null, chosenIds, Now);
        _galleries.GetByIdAsync(gallery.Id, Arg.Any<CancellationToken>()).Returns(gallery);
        _galleries.GetByBookingIdAsync(_booking.Id, Arg.Any<CancellationToken>()).Returns(gallery);
        return gallery;
    }

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
