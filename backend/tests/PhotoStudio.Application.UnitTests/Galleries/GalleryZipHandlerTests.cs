using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Galleries;
using PhotoStudio.Application.Galleries.Finals;
using PhotoStudio.Application.Galleries.Selection;
using PhotoStudio.Application.Portal.Galleries;
using PhotoStudio.Application.Storage;
using PhotoStudio.Application.UnitTests.Portal;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.UnitTests.Galleries;

/// <summary>
/// Tests of the ZIP the photographer makes by hand, of the details of each final, and of the downloads of the client, which are
/// counted because the client asks for the addresses at the moment they download.
/// </summary>
public sealed class GalleryZipHandlerTests
{
    private readonly PortalFixture _fixture = new();
    private readonly IGalleryRepository _galleries = Substitute.For<IGalleryRepository>();
    private readonly IObjectStorage _storage = Substitute.For<IObjectStorage>();
    private readonly FixedTimeProvider _clock = new(PortalFixture.Now);

    /// <summary>
    /// Initializes a new instance of the <see cref="GalleryZipHandlerTests"/> class: every address the storage signs is a fresh one.
    /// </summary>
    public GalleryZipHandlerTests()
    {
        _storage.CreateDownloadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => new PresignedUrl(new Uri($"https://storage.test/{call.ArgAt<string>(0)}?sig=secret"), PortalFixture.Now.Add(call.ArgAt<TimeSpan>(1)), new Dictionary<string, string>()));
        _storage.CreateUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(call => new PresignedUrl(new Uri($"https://storage.test/{call.ArgAt<string>(0)}?sig=up"), PortalFixture.Now.AddMinutes(15), new Dictionary<string, string> { ["Content-Type"] = call.ArgAt<string>(1) }));
    }

    /// <summary>
    /// The photographer gets a place to upload the ZIP once the finals are published, signed for the type of a ZIP.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RequestZipUpload_GivesAPlace_WhenTheFinalsArePublished()
    {
        var gallery = MakeGallery(GalleryStatus.Ready);

        var slot = await new RequestZipUploadHandler(_galleries, _storage)
            .HandleAsync(new RequestZipUploadCommand(gallery.PhotographerId, gallery.Id, 300_000_000), TestContext.Current.CancellationToken);

        slot.Url.ToString().ShouldContain($"galleries/{gallery.Id:D}/zip/photos.zip");
        slot.Headers["Content-Type"].ShouldBe("application/zip");
    }

    /// <summary>
    /// A ZIP is refused before the finals are published, when it is empty or over 500 MB, and for a gallery of another photographer.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RequestZipUpload_RefusesWhatIsNotValid()
    {
        var editing = MakeGallery(GalleryStatus.InEditing);
        var handler = new RequestZipUploadHandler(_galleries, _storage);
        (await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(new RequestZipUploadCommand(editing.PhotographerId, editing.Id, 100), TestContext.Current.CancellationToken)))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);

        var ready = MakeGallery(GalleryStatus.Ready);
        (await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(new RequestZipUploadCommand(ready.PhotographerId, ready.Id, 0), TestContext.Current.CancellationToken)))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidZip);
        (await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(new RequestZipUploadCommand(ready.PhotographerId, ready.Id, Gallery.MaxZipBytes + 1), TestContext.Current.CancellationToken)))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidZip);
        await Should.ThrowAsync<NotFoundException>(() => handler.HandleAsync(new RequestZipUploadCommand(Guid.CreateVersion7(), ready.Id, 100), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Registering looks at the real object: a ZIP within the limit is kept and saved.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RegisterZip_KeepsAValidZip()
    {
        var gallery = MakeGallery(GalleryStatus.Ready);
        _storage.GetInfoAsync(GalleryKeys.Zip(gallery.Id), Arg.Any<CancellationToken>()).Returns(new StoredObjectInfo(310_000_000, "application/zip"));

        var response = await new RegisterZipHandler(_galleries, _storage, _clock)
            .HandleAsync(new RegisterZipCommand(gallery.PhotographerId, gallery.Id), TestContext.Current.CancellationToken);

        response.Zip.ShouldNotBeNull().Bytes.ShouldBe(310_000_000);
        await _galleries.Received(1).UpdateAsync(gallery, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A file that is missing, is not a ZIP or weighs too much is refused; what is not a ZIP is deleted, and so is the old ZIP it
    /// replaced, because the object it pointed to was overwritten.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RegisterZip_RefusesAndDeletesWhatIsNotAZip()
    {
        var gallery = MakeGallery(GalleryStatus.Delivered);
        gallery.RegisterZip(1_000, PortalFixture.Now);
        var handler = new RegisterZipHandler(_galleries, _storage, _clock);
        _storage.GetInfoAsync(GalleryKeys.Zip(gallery.Id), Arg.Any<CancellationToken>()).Returns((StoredObjectInfo?)null);
        (await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(new RegisterZipCommand(gallery.PhotographerId, gallery.Id), TestContext.Current.CancellationToken)))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidZip);

        _storage.GetInfoAsync(GalleryKeys.Zip(gallery.Id), Arg.Any<CancellationToken>()).Returns(new StoredObjectInfo(100, "text/html"));
        (await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(new RegisterZipCommand(gallery.PhotographerId, gallery.Id), TestContext.Current.CancellationToken)))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidZip);

        await _storage.Received(1).DeleteAsync(GalleryKeys.Zip(gallery.Id), Arg.Any<CancellationToken>());
        gallery.HasZip.ShouldBeFalse();
    }

    /// <summary>
    /// Deleting the ZIP forgets it in the gallery and removes the file, in that order; deleting what is not there does nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RemoveZip_ForgetsItAndDeletesTheFile_OnlyWhenThereIsOne()
    {
        var gallery = MakeGallery(GalleryStatus.Delivered);
        var handler = new RemoveZipHandler(_galleries, _storage, _clock);
        await handler.HandleAsync(new RemoveZipCommand(gallery.PhotographerId, gallery.Id), TestContext.Current.CancellationToken);
        await _storage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        gallery.RegisterZip(500, PortalFixture.Now);
        var response = await handler.HandleAsync(new RemoveZipCommand(gallery.PhotographerId, gallery.Id), TestContext.Current.CancellationToken);

        response.Zip.ShouldBeNull();
        await _galleries.Received(1).UpdateAsync(gallery, Arg.Any<CancellationToken>());
        await _storage.Received(1).DeleteAsync(GalleryKeys.Zip(gallery.Id), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The photographer gets an address to download a final, named after the original file, without it counting as a download of the
    /// client; a photo without a final is not found.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheFinalDownloadOfThePhotographer_IsNotCounted()
    {
        var gallery = MakeGallery(GalleryStatus.Delivered);
        var handler = new GetFinalDownloadHandler(_galleries, _storage);

        var download = await handler.HandleAsync(new GetFinalDownloadQuery(gallery.PhotographerId, gallery.Id, gallery.PackagePhotoIds[0]), TestContext.Current.CancellationToken);

        download.FileName.ShouldBe("IMG_0000.jpg");
        gallery.Photos.Single(photo => photo.Id == gallery.PackagePhotoIds[0]).FinalDownloads.ShouldBe(0);
        await Should.ThrowAsync<NotFoundException>(() => handler.HandleAsync(new GetFinalDownloadQuery(gallery.PhotographerId, gallery.Id, gallery.ExtraPhotoIds[0]), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The client gets the addresses of the photos they ask for, with the name of the file and five minutes to use them, and every
    /// photo asked for is counted.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheClient_AsksForTheAddresses_AndEachOneIsCounted()
    {
        var gallery = MakeGallery(GalleryStatus.Delivered);

        var response = await PortalDownloads().HandleAsync(
            new RequestPortalDownloadsCommand(PortalFixture.Token, _fixture.Booking.Id, [.. gallery.PackagePhotoIds]), TestContext.Current.CancellationToken);

        response.Items.Select(item => item.FileName).ShouldBe(["IMG_0000.jpg", "IMG_0001.jpg"]);
        response.Items.ShouldAllBe(item => item.ExpiresAt == PortalFixture.Now.AddMinutes(5));
        gallery.Photos.Where(photo => gallery.PackagePhotoIds.Contains(photo.Id)).ShouldAllBe(photo => photo.FinalDownloads == 1);
        await _galleries.Received(1).UpdateAsync(gallery, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A photo the client may not download, such as an extra that was not released, makes the request fail and counts nothing; so do
    /// a session of another booking and a request without photos.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheClient_CannotAskForWhatIsNotTheirs_AndNothingIsCounted()
    {
        var gallery = MakeGallery(GalleryStatus.Delivered);
        gallery.RegisterFinal(gallery.ExtraPhotoIds[0], 100, PortalFixture.Now);
        var handler = PortalDownloads();

        (await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(
            new RequestPortalDownloadsCommand(PortalFixture.Token, _fixture.Booking.Id, [gallery.PackagePhotoIds[0], gallery.ExtraPhotoIds[0]]), TestContext.Current.CancellationToken)))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidPhoto);
        (await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(
            new RequestPortalDownloadsCommand(PortalFixture.Token, _fixture.Booking.Id, []), TestContext.Current.CancellationToken)))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidPhoto);
        await Should.ThrowAsync<NotFoundException>(() => handler.HandleAsync(
            new RequestPortalDownloadsCommand(PortalFixture.Token, Guid.CreateVersion7(), [gallery.PackagePhotoIds[0]]), TestContext.Current.CancellationToken));

        gallery.Photos.ShouldAllBe(photo => photo.FinalDownloads == 0);
        await _galleries.DidNotReceive().UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// If two downloads collide, the gallery is loaded again and the count is written on the fresh copy, so none is lost.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task WhenCountingCollides_ItIsWrittenAgainOnTheFreshGallery()
    {
        var stale = MakeGallery(GalleryStatus.Delivered);
        var fresh = Gallery.Restore(
            stale.Id, stale.Version + 1, stale.PhotographerId, stale.BookingId, stale.Snapshot, GalleryStatus.Delivered, 2, 0, stale.ExtraPhotoPrice, PortalFixture.Now, PortalFixture.Now,
            stale.Photos, null, stale.SelectedPhotoIds, PortalFixture.Now);
        _galleries.GetByBookingIdAsync(_fixture.Booking.Id, Arg.Any<CancellationToken>()).Returns(stale, fresh);
        _galleries.UpdateAsync(stale, Arg.Any<CancellationToken>()).Returns(Task.FromException(new ConflictException(ApplicationErrorCodes.ConcurrencyConflict, "x")));

        await PortalDownloads().HandleAsync(
            new RequestPortalDownloadsCommand(PortalFixture.Token, _fixture.Booking.Id, [stale.PackagePhotoIds[0]]), TestContext.Current.CancellationToken);

        fresh.Photos.Single(photo => photo.Id == stale.PackagePhotoIds[0]).FinalDownloads.ShouldBe(1);
        await _galleries.Received(1).UpdateAsync(fresh, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The client gets the address of the ZIP only when the gallery is delivered and has one, and it is counted with its instant.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheClient_GetsTheZip_WhenThereIsOne_AndItIsCounted()
    {
        var gallery = MakeGallery(GalleryStatus.Delivered);
        var handler = new RequestPortalZipDownloadHandler(_galleries, _fixture.Links, _fixture.Tokens, _storage, _fixture.Clock);
        var command = new RequestPortalZipDownloadCommand(PortalFixture.Token, _fixture.Booking.Id);
        (await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(command, TestContext.Current.CancellationToken))).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);

        gallery.RegisterZip(300_000_000, PortalFixture.Now);
        var response = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        response.FileName.ShouldBe("PhotoSense-fotos.zip");
        response.Bytes.ShouldBe(300_000_000);
        response.Url.ToString().ShouldContain($"galleries/{gallery.Id:D}/zip/photos.zip");
        gallery.ZipDownloads.ShouldBe(1);
        await Should.ThrowAsync<NotFoundException>(() => handler.HandleAsync(command with { AuthorizedBookingId = Guid.CreateVersion7() }, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// What the client reads of the gallery carries no address at all: the finals are listed by name and size, the extras only once
    /// released, and the ZIP only with its size, so nothing can be downloaded without being counted.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheGalleryOfTheClient_HasNoAddressesForTheFinals()
    {
        var gallery = MakeGallery(GalleryStatus.Delivered);
        gallery.RegisterFinal(gallery.ExtraPhotoIds[0], 100, PortalFixture.Now);
        gallery.RegisterZip(300_000_000, PortalFixture.Now);

        var unreleased = await gallery.ToPortalResponseAsync(_storage, TestContext.Current.CancellationToken);
        gallery.ReleaseExtras(PortalFixture.Now);
        var released = await gallery.ToPortalResponseAsync(_storage, TestContext.Current.CancellationToken);

        unreleased.Finals.ShouldNotBeNull().Select(final => final.FileName).ShouldBe(["IMG_0000.jpg", "IMG_0001.jpg"]);
        unreleased.Zip.ShouldNotBeNull().Bytes.ShouldBe(300_000_000);
        released.Finals.ShouldNotBeNull().Select(final => final.IsExtra).ShouldBe([false, false, true]);
        typeof(PortalFinalResponse).GetProperties().Select(property => property.Name).ShouldNotContain("Url");
        await _storage.DidNotReceive().CreateDownloadUrlAsync(Arg.Is<string>(key => key.Contains("/finals/") || key.Contains("/zip/")), Arg.Any<TimeSpan>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The browser of the photographer sends the size in pixels and the date, and they reach the gallery with the final.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RegisteringFinals_KeepsTheDetailsTheBrowserSent()
    {
        var gallery = MakeGallery(GalleryStatus.InEditing);
        var photoId = gallery.PackagePhotoIds[0];
        var taken = PortalFixture.Now.AddDays(-20);
        _storage.GetInfoAsync(GalleryKeys.Final(gallery.Id, photoId), Arg.Any<CancellationToken>()).Returns(new StoredObjectInfo(4_000_000, "image/jpeg"));

        await new RegisterFinalsHandler(_galleries, _storage, _clock).HandleAsync(
            new RegisterFinalsCommand(gallery.PhotographerId, gallery.Id, [photoId], [new FinalDetailsEntry(photoId, 1250, 3000, taken)]), TestContext.Current.CancellationToken);

        var photo = gallery.Photos.Single(candidate => candidate.Id == photoId);
        (photo.FinalWidth, photo.FinalHeight, photo.FinalCapturedAt).ShouldBe((1250, 3000, taken));
    }

    /// <summary>
    /// The client sees a small image of each final that has one, without it counting as a download, and none for a final without it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheClient_SeesTheThumbnailOfAFinal_WithoutItBeingADownload()
    {
        var gallery = MakeGallery(GalleryStatus.Delivered, thumbOnFirst: true);

        var response = await gallery.ToPortalResponseAsync(_storage, TestContext.Current.CancellationToken);

        var finals = response.Finals.ShouldNotBeNull();
        finals[0].ThumbUrl.ShouldNotBeNull().ToString().ShouldContain($"galleries/{gallery.Id:D}/thumbs/{gallery.PackagePhotoIds[0]:D}.jpg");
        finals[1].ThumbUrl.ShouldBeNull();
        gallery.Photos.ShouldAllBe(photo => photo.FinalDownloads == 0);
    }

    /// <summary>
    /// The client looks at a final in its real quality, as edited: the address is of the final itself and carries no file name, so it
    /// is shown and not saved, and looking is not counted as a download.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheClient_LooksAtTheRealFinal_WithoutItBeingADownload()
    {
        var gallery = MakeGallery(GalleryStatus.Delivered);
        var handler = new GetPortalFinalViewHandler(_galleries, _fixture.Links, _fixture.Tokens, _storage, _fixture.Clock);

        var view = await handler.HandleAsync(new GetPortalFinalViewQuery(PortalFixture.Token, _fixture.Booking.Id, gallery.PackagePhotoIds[0]), TestContext.Current.CancellationToken);

        view.Url.ToString().ShouldContain($"galleries/{gallery.Id:D}/finals/{gallery.PackagePhotoIds[0]:D}.jpg");
        view.ExpiresAt.ShouldBe(PortalFixture.Now.AddMinutes(5));
        await _storage.Received(1).CreateDownloadUrlAsync(GalleryKeys.Final(gallery.Id, gallery.PackagePhotoIds[0]), TimeSpan.FromMinutes(5), null, Arg.Any<CancellationToken>());
        gallery.Photos.ShouldAllBe(photo => photo.FinalDownloads == 0);
        await _galleries.DidNotReceive().UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The client cannot look at what they may not take: an extra that was not released, a photo without a final, a gallery that is
    /// not delivered, or with the session of another booking.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheClient_CannotLookAtWhatIsNotTheirs()
    {
        var gallery = MakeGallery(GalleryStatus.Delivered);
        gallery.RegisterFinal(gallery.ExtraPhotoIds[0], 100, PortalFixture.Now);
        var handler = new GetPortalFinalViewHandler(_galleries, _fixture.Links, _fixture.Tokens, _storage, _fixture.Clock);

        (await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(new GetPortalFinalViewQuery(PortalFixture.Token, _fixture.Booking.Id, gallery.ExtraPhotoIds[0]), TestContext.Current.CancellationToken)))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidPhoto);
        (await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(new GetPortalFinalViewQuery(PortalFixture.Token, _fixture.Booking.Id, Guid.CreateVersion7()), TestContext.Current.CancellationToken)))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidPhoto);
        await Should.ThrowAsync<NotFoundException>(() => handler.HandleAsync(new GetPortalFinalViewQuery(PortalFixture.Token, Guid.CreateVersion7(), gallery.PackagePhotoIds[0]), TestContext.Current.CancellationToken));

        var ready = MakeGallery(GalleryStatus.Ready);
        (await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(new GetPortalFinalViewQuery(PortalFixture.Token, _fixture.Booking.Id, ready.PackagePhotoIds[0]), TestContext.Current.CancellationToken)))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
    }

    /// <summary>
    /// The photographer sees, for every chosen photo, a small image: the thumbnail of its final when it has one, and otherwise its
    /// proof, which has the watermark.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ThePhotographerSelection_ShowsTheThumbnailOrTheProof()
    {
        var gallery = MakeGallery(GalleryStatus.Delivered, thumbOnFirst: true);

        var response = await new GetSelectionHandler(_galleries, _storage).HandleAsync(new GetSelectionQuery(gallery.PhotographerId, gallery.Id), TestContext.Current.CancellationToken);

        response.Photos[0].ThumbIsFinal.ShouldBeTrue();
        response.Photos[0].ThumbUrl.ShouldNotBeNull().ToString().ShouldContain("/thumbs/");
        response.Photos[1].ThumbIsFinal.ShouldBeFalse();
        response.Photos[1].ThumbUrl.ShouldNotBeNull().ToString().ShouldContain("/proofs/");
        response.Photos[2].ThumbUrl.ShouldNotBeNull().ToString().ShouldContain("/proofs/");
    }

    /// <summary>
    /// Registering a final also takes its thumbnail when one was uploaded; one that is not a JPEG or weighs too much is deleted and does
    /// not stop the final from being registered.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RegisteringFinals_TakesTheThumbnail_AndIgnoresABadOne()
    {
        var gallery = MakeGallery(GalleryStatus.InEditing);
        var first = gallery.PackagePhotoIds[0];
        var second = gallery.PackagePhotoIds[1];
        _storage.GetInfoAsync(GalleryKeys.Final(gallery.Id, first), Arg.Any<CancellationToken>()).Returns(new StoredObjectInfo(4_000_000, "image/jpeg"));
        _storage.GetInfoAsync(GalleryKeys.Final(gallery.Id, second), Arg.Any<CancellationToken>()).Returns(new StoredObjectInfo(4_000_000, "image/jpeg"));
        _storage.GetInfoAsync(GalleryKeys.Thumb(gallery.Id, first), Arg.Any<CancellationToken>()).Returns(new StoredObjectInfo(90_000, "image/jpeg"));
        _storage.GetInfoAsync(GalleryKeys.Thumb(gallery.Id, second), Arg.Any<CancellationToken>()).Returns(new StoredObjectInfo(5_000_000, "image/jpeg"));

        var response = await new RegisterFinalsHandler(_galleries, _storage, _clock)
            .HandleAsync(new RegisterFinalsCommand(gallery.PhotographerId, gallery.Id, [first, second]), TestContext.Current.CancellationToken);

        response.Rejected.ShouldBeEmpty();
        gallery.Photos.Single(photo => photo.Id == first).FinalThumbBytes.ShouldBe(90_000);
        gallery.Photos.Single(photo => photo.Id == second).FinalThumbBytes.ShouldBeNull();
        gallery.HasFinal(second).ShouldBeTrue();
        await _storage.Received(1).DeleteAsync(GalleryKeys.Thumb(gallery.Id, second), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Every place to upload a final comes with a second one for its thumbnail, signed for a JPEG.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task EachPlaceToUploadAFinal_HasAnotherForItsThumbnail()
    {
        var gallery = MakeGallery(GalleryStatus.InEditing);

        var response = await new RequestFinalUploadsHandler(_galleries, _storage).HandleAsync(
            new RequestFinalUploadsCommand(gallery.PhotographerId, gallery.Id, [new FinalFile(gallery.PackagePhotoIds[0], 4_000_000)]), TestContext.Current.CancellationToken);

        var slot = response.Slots.Single();
        slot.Url.ToString().ShouldContain("/finals/");
        slot.ThumbUrl.ShouldNotBeNull().ToString().ShouldContain("/thumbs/");
        slot.ThumbHeaders.ShouldNotBeNull()["Content-Type"].ShouldBe("image/jpeg");
    }

    /// <summary>
    /// Builds the handler that gives the client the addresses.
    /// </summary>
    /// <returns>The handler.</returns>
    private RequestPortalDownloadsHandler PortalDownloads() => new(_galleries, _fixture.Links, _fixture.Tokens, _storage, _fixture.Clock);

    /// <summary>
    /// Builds a gallery of the booking of the fixture: 4 proofs, 3 chosen (2 of the package and 1 extra), the package with its finals.
    /// </summary>
    /// <param name="status">State of the gallery.</param>
    /// <param name="thumbOnFirst">Whether the final of the first chosen photo already has its thumbnail.</param>
    /// <returns>The gallery, which the repository returns.</returns>
    private Gallery MakeGallery(GalleryStatus status, bool thumbOnFirst = false)
    {
        var proofs = Enumerable.Range(0, 4).Select(index => new GalleryPhoto(Guid.CreateVersion7(), $"IMG_{index:0000}.CR2", 10, PortalFixture.Now)).ToList();
        var chosen = proofs.Take(3).Select(photo => photo.Id).ToList();
        if (status != GalleryStatus.InEditing)
        {
            proofs = [.. proofs.Select(photo => chosen.Take(2).Contains(photo.Id) ? photo with { FinalBytes = 100, FinalUploadedAt = PortalFixture.Now, FinalThumbBytes = thumbOnFirst && photo.Id == chosen[0] ? 90_000 : null } : photo)];
        }

        var gallery = Gallery.Restore(
            Guid.CreateVersion7(), 3, _fixture.Booking.PhotographerId, _fixture.Booking.Id, new GallerySnapshot("María Pérez", "Retrato familiar", PortalFixture.Now),
            status, 2, 0, Money.Create(3_000m, "CRC"), PortalFixture.Now, PortalFixture.Now, proofs, null, chosen, PortalFixture.Now);
        _galleries.GetByIdAsync(gallery.Id, Arg.Any<CancellationToken>()).Returns(gallery);
        _galleries.GetByBookingIdAsync(_fixture.Booking.Id, Arg.Any<CancellationToken>()).Returns(gallery);
        return gallery;
    }
}
