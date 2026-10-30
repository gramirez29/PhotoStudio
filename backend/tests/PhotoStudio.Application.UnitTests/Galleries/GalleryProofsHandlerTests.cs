using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Galleries;
using PhotoStudio.Application.Galleries.Proofs;
using PhotoStudio.Application.Storage;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.UnitTests.Galleries;

/// <summary>
/// Tests of the use cases of the proofs: asking for places to upload, registering what was uploaded, listing, removing, and opening
/// the selection.
/// </summary>
public sealed class GalleryProofsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly IGalleryRepository _galleries = Substitute.For<IGalleryRepository>();
    private readonly IObjectStorage _storage = Substitute.For<IObjectStorage>();
    private readonly Gallery _gallery = NewGallery();

    /// <summary>
    /// Initializes a new instance of the <see cref="GalleryProofsHandlerTests"/> class with a gallery that is waiting for its proofs.
    /// </summary>
    public GalleryProofsHandlerTests()
    {
        _galleries.GetByIdAsync(_gallery.Id, Arg.Any<CancellationToken>()).Returns(_gallery);
        _storage.CreateUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(call => new PresignedUrl(new Uri($"https://storage.test/{call.ArgAt<string>(0)}?sig=secret"), Now.AddMinutes(15), new Dictionary<string, string> { ["Content-Type"] = call.ArgAt<string>(1) }));
        _storage.CreateDownloadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => new PresignedUrl(new Uri($"https://storage.test/{call.ArgAt<string>(0)}?get=secret"), Now.AddMinutes(10), new Dictionary<string, string>()));
    }

    /// <summary>
    /// Each file gets its own place to upload, on a key built from identifiers the backend generated, for a JPEG, for the longest time allowed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RequestUploads_GivesOneSlotPerFile_OnAKeyOfOurOwn()
    {
        var response = await RequestHandler().HandleAsync(
            new RequestProofUploadsCommand(_gallery.PhotographerId, _gallery.Id, [new ProofFile("../../etc/passwd", 1000), new ProofFile("b.jpg", 2000)]),
            TestContext.Current.CancellationToken);

        response.Slots.Count.ShouldBe(2);
        response.Slots.Select(slot => slot.PhotoId).Distinct().Count().ShouldBe(2);
        foreach (var slot in response.Slots)
        {
            slot.Headers["Content-Type"].ShouldBe("image/jpeg");
            slot.Url.AbsolutePath.ShouldBe($"/galleries/{_gallery.Id}/proofs/{slot.PhotoId}.jpg");
            await _storage.Received(1).CreateUploadUrlAsync(GalleryKeys.Proof(_gallery.Id, slot.PhotoId), "image/jpeg", ObjectStorageLimits.MaxUploadLifetime, Arg.Any<CancellationToken>());
        }

        await _galleries.DidNotReceive().UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A request that would make the gallery hold more than 150, one with no files, one with more than 50, and one with a file that
    /// is not valid are refused before any address is made.
    /// </summary>
    /// <param name="scenario">What is wrong.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("no-files")]
    [InlineData("too-many-files")]
    [InlineData("empty-file")]
    [InlineData("too-big")]
    [InlineData("no-name")]
    [InlineData("does-not-fit")]
    public async Task RequestUploads_WithAnInvalidRequest_IsRefusedBeforeAnyAddressIsMade(string scenario)
    {
        var gallery = scenario == "does-not-fit" ? NewGallery(photos: 148) : _gallery;
        _galleries.GetByIdAsync(gallery.Id, Arg.Any<CancellationToken>()).Returns(gallery);
        var files = scenario switch
        {
            "no-files" => new List<ProofFile>(),
            "too-many-files" => Enumerable.Range(0, 51).Select(index => new ProofFile($"{index}.jpg", 10)).ToList(),
            "empty-file" => [new ProofFile("a.jpg", 0)],
            "too-big" => [new ProofFile("a.jpg", GalleryPhoto.MaxProofBytes + 1)],
            "no-name" => [new ProofFile("  ", 10)],
            _ => [new ProofFile("a.jpg", 10), new ProofFile("b.jpg", 10), new ProofFile("c.jpg", 10)],
        };

        var error = await Should.ThrowAsync<DomainException>(() => RequestHandler().HandleAsync(
            new RequestProofUploadsCommand(gallery.PhotographerId, gallery.Id, files), TestContext.Current.CancellationToken));

        error.Code.ShouldBeOneOf(DomainErrorCodes.GalleryInvalidPhoto, DomainErrorCodes.GalleryFull);
        await _storage.DidNotReceive().CreateUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A gallery whose selection is open takes no more proofs, and one of another photographer is not found.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RequestUploads_AfterTheSelectionOpened_OrOfAnotherPhotographer_IsRefused()
    {
        var open = NewGallery(GalleryStatus.InSelection, 1);
        _galleries.GetByIdAsync(open.Id, Arg.Any<CancellationToken>()).Returns(open);

        (await Should.ThrowAsync<DomainException>(() => RequestHandler().HandleAsync(
            new RequestProofUploadsCommand(open.PhotographerId, open.Id, [new ProofFile("a.jpg", 10)]), TestContext.Current.CancellationToken)))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
        await Should.ThrowAsync<NotFoundException>(() => RequestHandler().HandleAsync(
            new RequestProofUploadsCommand(Guid.CreateVersion7(), _gallery.Id, [new ProofFile("a.jpg", 10)]), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// An upload that really is in the storage as a JPEG of a valid size is registered, and the gallery is saved once.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WhatWasUploaded_IsRegistered()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        StorageHas(first, 5000);
        StorageHas(second, GalleryPhoto.MaxProofBytes);

        var response = await RegisterHandler().HandleAsync(
            new RegisterProofsCommand(_gallery.PhotographerId, _gallery.Id, [new ProofRegistration(first, "IMG_1.jpg"), new ProofRegistration(second, "IMG_2.jpg")]),
            TestContext.Current.CancellationToken);

        response.Rejected.ShouldBeEmpty();
        response.Gallery.PhotoCount.ShouldBe(2);
        _gallery.Photos.Select(photo => (photo.Id, photo.OriginalName, photo.ProofBytes)).ShouldBe([(first, "IMG_1.jpg", 5000L), (second, "IMG_2.jpg", GalleryPhoto.MaxProofBytes)]);
        await _galleries.Received(1).UpdateAsync(_gallery, Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// What is not in the storage, is not a JPEG, is empty, is too big or has a bad name is rejected with its reason, and the object
    /// is deleted, so nothing that was not meant to be published stays in the storage.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WhatIsNotValid_IsRejectedWithItsReason_AndDeleted()
    {
        var missing = Guid.CreateVersion7();
        var wrongType = Guid.CreateVersion7();
        var empty = Guid.CreateVersion7();
        var tooBig = Guid.CreateVersion7();
        var badName = Guid.CreateVersion7();
        var good = Guid.CreateVersion7();
        _storage.GetInfoAsync(GalleryKeys.Proof(_gallery.Id, missing), Arg.Any<CancellationToken>()).Returns((StoredObjectInfo?)null);
        _storage.GetInfoAsync(GalleryKeys.Proof(_gallery.Id, wrongType), Arg.Any<CancellationToken>()).Returns(new StoredObjectInfo(100, "text/html"));
        StorageHas(empty, 0);
        StorageHas(tooBig, GalleryPhoto.MaxProofBytes + 1);
        StorageHas(badName, 100);
        StorageHas(good, 100);

        var response = await RegisterHandler().HandleAsync(
            new RegisterProofsCommand(
                _gallery.PhotographerId,
                _gallery.Id,
                [
                    new ProofRegistration(missing, "a.jpg"),
                    new ProofRegistration(wrongType, "b.jpg"),
                    new ProofRegistration(empty, "c.jpg"),
                    new ProofRegistration(tooBig, "d.jpg"),
                    new ProofRegistration(badName, "bad\nname.jpg"),
                    new ProofRegistration(good, "good.jpg"),
                ]),
            TestContext.Current.CancellationToken);

        response.Rejected.Select(rejection => (rejection.PhotoId, rejection.Reason)).ShouldBe(
            [(missing, "missing"), (wrongType, "wrong_type"), (empty, "empty"), (tooBig, "too_big"), (badName, "invalid_name")]);
        response.Gallery.PhotoCount.ShouldBe(1);
        _gallery.Photos.Single().Id.ShouldBe(good);
        foreach (var rejected in new[] { missing, wrongType, empty, tooBig, badName })
        {
            await _storage.Received(1).DeleteAsync(GalleryKeys.Proof(_gallery.Id, rejected), Arg.Any<CancellationToken>());
        }

        await _storage.DidNotReceive().DeleteAsync(GalleryKeys.Proof(_gallery.Id, good), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Registering what is already registered changes nothing, rejects nothing and deletes nothing: a repeated request is safe.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_ThePhotosThatAreAlreadyRegistered_ChangesNothing()
    {
        var id = Guid.CreateVersion7();
        StorageHas(id, 100);
        var command = new RegisterProofsCommand(_gallery.PhotographerId, _gallery.Id, [new ProofRegistration(id, "a.jpg")]);
        await RegisterHandler().HandleAsync(command, TestContext.Current.CancellationToken);
        _galleries.ClearReceivedCalls();
        _storage.ClearReceivedCalls();

        var again = await RegisterHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        again.Rejected.ShouldBeEmpty();
        again.Gallery.PhotoCount.ShouldBe(1);
        await _galleries.DidNotReceive().UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A photo that does not fit in the gallery is rejected as full and its object deleted, while the ones that fit are kept.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WhatDoesNotFit_IsRejectedAsFull()
    {
        var full = NewGallery(photos: 149);
        _galleries.GetByIdAsync(full.Id, Arg.Any<CancellationToken>()).Returns(full);
        var fits = Guid.CreateVersion7();
        var overflow = Guid.CreateVersion7();
        StorageHasFor(full, fits, 100);
        StorageHasFor(full, overflow, 100);

        var response = await RegisterHandler().HandleAsync(
            new RegisterProofsCommand(full.PhotographerId, full.Id, [new ProofRegistration(fits, "a.jpg"), new ProofRegistration(overflow, "b.jpg")]),
            TestContext.Current.CancellationToken);

        response.Rejected.ShouldBe([new ProofRejection(overflow, "gallery_full")]);
        full.Photos.Count.ShouldBe(150);
        await _storage.Received(1).DeleteAsync(GalleryKeys.Proof(full.Id, overflow), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Two batches registered at once may collide on the version of the gallery: the second reloads it and registers again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WhenTheGalleryChangedMeanwhile_TriesAgainOnTheFreshOne()
    {
        var id = Guid.CreateVersion7();
        StorageHas(id, 100);
        var fresh = Gallery.Restore(_gallery.Id, 2, _gallery.PhotographerId, _gallery.BookingId, _gallery.Snapshot, GalleryStatus.Pending, 15, 0, _gallery.ExtraPhotoPrice, Now, Now);
        _galleries.GetByIdAsync(_gallery.Id, Arg.Any<CancellationToken>()).Returns(_gallery, fresh);
        _galleries.UpdateAsync(_gallery, Arg.Any<CancellationToken>()).Returns(Task.FromException(new ConflictException(ApplicationErrorCodes.ConcurrencyConflict, "x")));

        var response = await RegisterHandler().HandleAsync(
            new RegisterProofsCommand(_gallery.PhotographerId, _gallery.Id, [new ProofRegistration(id, "a.jpg")]), TestContext.Current.CancellationToken);

        response.Gallery.PhotoCount.ShouldBe(1);
        fresh.Photos.Single().Id.ShouldBe(id);
        await _galleries.Received(1).UpdateAsync(fresh, Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// If the gallery keeps changing, the request fails with a conflict and deletes nothing: the uploads are still good.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WhenTheCollisionsNeverEnd_FailsWithAConflict_AndDeletesNothing()
    {
        var id = Guid.CreateVersion7();
        StorageHas(id, 100);
        // Every reload gives a fresh copy, as the database does: with the same object the second attempt would find the photo already added.
        _galleries.GetByIdAsync(_gallery.Id, Arg.Any<CancellationToken>()).Returns(_ => Gallery.Restore(_gallery.Id, 1, _gallery.PhotographerId, _gallery.BookingId, _gallery.Snapshot, GalleryStatus.Pending, 15, 0, _gallery.ExtraPhotoPrice, Now, Now));
        _galleries.UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new ConflictException(ApplicationErrorCodes.ConcurrencyConflict, "x")));

        await Should.ThrowAsync<ConflictException>(() => RegisterHandler().HandleAsync(
            new RegisterProofsCommand(_gallery.PhotographerId, _gallery.Id, [new ProofRegistration(id, "a.jpg")]), TestContext.Current.CancellationToken));

        await _storage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A request with no uploads or too many, one for a gallery whose selection is open, and one for another photographer's gallery
    /// are refused.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_InvalidRequests_AreRefused()
    {
        var open = NewGallery(GalleryStatus.InSelection, 1);
        _galleries.GetByIdAsync(open.Id, Arg.Any<CancellationToken>()).Returns(open);
        var id = Guid.CreateVersion7();

        await Should.ThrowAsync<DomainException>(() => RegisterHandler().HandleAsync(new RegisterProofsCommand(_gallery.PhotographerId, _gallery.Id, []), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<DomainException>(() => RegisterHandler().HandleAsync(
            new RegisterProofsCommand(_gallery.PhotographerId, _gallery.Id, [.. Enumerable.Range(0, 51).Select(_ => new ProofRegistration(Guid.CreateVersion7(), "a.jpg"))]),
            TestContext.Current.CancellationToken));
        await Should.ThrowAsync<DomainException>(() => RegisterHandler().HandleAsync(
            new RegisterProofsCommand(open.PhotographerId, open.Id, [new ProofRegistration(id, "a.jpg")]), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<NotFoundException>(() => RegisterHandler().HandleAsync(
            new RegisterProofsCommand(Guid.CreateVersion7(), _gallery.Id, [new ProofRegistration(id, "a.jpg")]), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The proofs are listed in order, each with a signed address that works for ten minutes, and only for the owner.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task List_GivesEachProofItsAddress_OnlyToTheOwner()
    {
        var gallery = NewGallery(photos: 2);
        _galleries.GetByIdAsync(gallery.Id, Arg.Any<CancellationToken>()).Returns(gallery);

        var response = await new ListProofsHandler(_galleries, _storage).HandleAsync(new ListProofsQuery(gallery.PhotographerId, gallery.Id), TestContext.Current.CancellationToken);

        response.Items.Select(item => item.Id).ShouldBe(gallery.Photos.Select(photo => photo.Id));
        response.Items[0].Url.AbsolutePath.ShouldBe($"/galleries/{gallery.Id}/proofs/{gallery.Photos[0].Id}.jpg");
        await _storage.Received(2).CreateDownloadUrlAsync(Arg.Any<string>(), ListProofsHandler.UrlLifetime, Arg.Is<string?>(name => name == null), Arg.Any<CancellationToken>());
        ListProofsHandler.UrlLifetime.ShouldBe(TimeSpan.FromMinutes(10));
        await Should.ThrowAsync<NotFoundException>(() =>
            new ListProofsHandler(_galleries, _storage).HandleAsync(new ListProofsQuery(Guid.CreateVersion7(), gallery.Id), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Removing a proof saves the gallery first and deletes the object after, and a photo that is not there is not found.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Remove_ForgetsThePhotoThenDeletesTheObject()
    {
        var gallery = NewGallery(photos: 2);
        _galleries.GetByIdAsync(gallery.Id, Arg.Any<CancellationToken>()).Returns(gallery);
        var doomed = gallery.Photos[0].Id;
        var order = new List<string>();
        _galleries.UpdateAsync(gallery, Arg.Any<CancellationToken>()).Returns(_ => { order.Add("saved"); return Task.CompletedTask; });
        _storage.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => { order.Add("deleted"); return Task.CompletedTask; });

        var response = await RemoveHandler().HandleAsync(new RemoveProofCommand(gallery.PhotographerId, gallery.Id, doomed), TestContext.Current.CancellationToken);

        response.PhotoCount.ShouldBe(1);
        order.ShouldBe(["saved", "deleted"]);
        await _storage.Received(1).DeleteAsync(GalleryKeys.Proof(gallery.Id, doomed), Arg.Any<CancellationToken>());
        await Should.ThrowAsync<NotFoundException>(() => RemoveHandler().HandleAsync(new RemoveProofCommand(gallery.PhotographerId, gallery.Id, Guid.CreateVersion7()), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// If the storage cannot delete the object, the photo is still gone: the object is only an orphan, and the request does not fail.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Remove_WhenTheStorageFails_StillRemovesThePhoto()
    {
        var gallery = NewGallery(photos: 1);
        _galleries.GetByIdAsync(gallery.Id, Arg.Any<CancellationToken>()).Returns(gallery);
        _storage.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new StorageUnavailableException("down")));

        var response = await RemoveHandler().HandleAsync(new RemoveProofCommand(gallery.PhotographerId, gallery.Id, gallery.Photos[0].Id), TestContext.Current.CancellationToken);

        response.PhotoCount.ShouldBe(0);
        await _galleries.Received(1).UpdateAsync(gallery, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A proof cannot be removed once the selection is open, and nothing is deleted.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Remove_AfterTheSelectionOpened_IsRefused()
    {
        var open = NewGallery(GalleryStatus.InSelection, 1);
        _galleries.GetByIdAsync(open.Id, Arg.Any<CancellationToken>()).Returns(open);

        await Should.ThrowAsync<DomainException>(() => RemoveHandler().HandleAsync(new RemoveProofCommand(open.PhotographerId, open.Id, open.Photos[0].Id), TestContext.Current.CancellationToken));

        await _storage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _galleries.DidNotReceive().UpdateAsync(Arg.Any<Gallery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Opening the selection saves the gallery in selection, and refuses one without proofs or one that is not the photographer's.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task OpenSelection_SavesTheGallery_AndRefusesWhatCannotOpen()
    {
        var withPhotos = NewGallery(photos: 2);
        _galleries.GetByIdAsync(withPhotos.Id, Arg.Any<CancellationToken>()).Returns(withPhotos);
        var handler = new OpenSelectionHandler(_galleries, new FixedTimeProvider(Now.AddHours(1)));

        var response = await handler.HandleAsync(new OpenSelectionCommand(withPhotos.PhotographerId, withPhotos.Id), TestContext.Current.CancellationToken);

        response.Status.ShouldBe(GalleryStatus.InSelection);
        await _galleries.Received(1).UpdateAsync(withPhotos, Arg.Any<CancellationToken>());
        (await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(new OpenSelectionCommand(_gallery.PhotographerId, _gallery.Id), TestContext.Current.CancellationToken)))
            .Code.ShouldBe(DomainErrorCodes.GalleryNoPhotos);
        await Should.ThrowAsync<NotFoundException>(() => handler.HandleAsync(new OpenSelectionCommand(Guid.CreateVersion7(), withPhotos.Id), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Makes the storage report a good JPEG of a size for a photo of the gallery under test.
    /// </summary>
    /// <param name="photoId">The photo.</param>
    /// <param name="bytes">Its size.</param>
    private void StorageHas(Guid photoId, long bytes) => StorageHasFor(_gallery, photoId, bytes);

    /// <summary>
    /// Makes the storage report a JPEG of a size for a photo of a gallery.
    /// </summary>
    /// <param name="gallery">The gallery.</param>
    /// <param name="photoId">The photo.</param>
    /// <param name="bytes">Its size.</param>
    private void StorageHasFor(Gallery gallery, Guid photoId, long bytes) =>
        _storage.GetInfoAsync(GalleryKeys.Proof(gallery.Id, photoId), Arg.Any<CancellationToken>()).Returns(new StoredObjectInfo(bytes, "image/jpeg"));

    /// <summary>Builds the handler that gives places to upload.</summary>
    /// <returns>The handler.</returns>
    private RequestProofUploadsHandler RequestHandler() => new(_galleries, _storage);

    /// <summary>Builds the handler that registers uploads.</summary>
    /// <returns>The handler.</returns>
    private RegisterProofsHandler RegisterHandler() => new(_galleries, _storage, new FixedTimeProvider(Now));

    /// <summary>Builds the handler that removes a proof.</summary>
    /// <returns>The handler.</returns>
    private RemoveProofHandler RemoveHandler() => new(_galleries, _storage, new FixedTimeProvider(Now));

    /// <summary>
    /// Builds a gallery with some proofs already registered.
    /// </summary>
    /// <param name="status">The state.</param>
    /// <param name="photos">How many proofs it holds.</param>
    /// <returns>The gallery.</returns>
    private static Gallery NewGallery(GalleryStatus status = GalleryStatus.Pending, int photos = 0) => Gallery.Restore(
        Guid.CreateVersion7(),
        1,
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        new GallerySnapshot("María Pérez", "Retrato Familiar", Now.AddDays(-1)),
        status,
        15,
        0,
        Money.Create(3_000m, "CRC"),
        Now,
        Now,
        Enumerable.Range(0, photos).Select(index => new GalleryPhoto(Guid.CreateVersion7(), $"{index}.jpg", 100, Now)));
}
