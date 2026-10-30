using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;
using PhotoStudio.Domain.Galleries.Events;

namespace PhotoStudio.Domain.UnitTests.Galleries;

/// <summary>
/// Tests of the proofs of a <see cref="Gallery"/>: registering them, removing them and opening the selection.
/// </summary>
public sealed class GalleryProofsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A proof is registered with the name of its file and its size, and the gallery remembers when.
    /// </summary>
    [Fact]
    public void RegisterProof_AddsThePhoto()
    {
        var gallery = NewGallery();
        var id = Guid.CreateVersion7();

        var added = gallery.RegisterProof(id, "  IMG_0001.jpg ", 120_000, Now.AddMinutes(5));

        added.ShouldBeTrue();
        gallery.Photos.ShouldBe([new GalleryPhoto(id, "IMG_0001.jpg", 120_000, Now.AddMinutes(5))]);
        gallery.UpdatedAt.ShouldBe(Now.AddMinutes(5));
    }

    /// <summary>
    /// Registering the same photo again does nothing: a request that is repeated must not duplicate it.
    /// </summary>
    [Fact]
    public void RegisterProof_TheSamePhotoTwice_IsRegisteredOnce()
    {
        var gallery = NewGallery();
        var id = Guid.CreateVersion7();
        gallery.RegisterProof(id, "a.jpg", 1000, Now);

        gallery.RegisterProof(id, "other-name.jpg", 2000, Now.AddHours(1)).ShouldBeFalse();

        gallery.Photos.Count.ShouldBe(1);
        gallery.Photos[0].OriginalName.ShouldBe("a.jpg");
        gallery.UpdatedAt.ShouldBe(Now);
    }

    /// <summary>
    /// A name that is empty, too long or has control characters, and a size outside the limits, are refused.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="bytes">The size.</param>
    [Theory]
    [InlineData("", 1000)]
    [InlineData("   ", 1000)]
    [InlineData(null, 1000)]
    [InlineData("a\nb.jpg", 1000)]
    [InlineData("a\u0000b.jpg", 1000)]
    [InlineData("fine.jpg", 0)]
    [InlineData("fine.jpg", -5)]
    [InlineData("fine.jpg", GalleryPhoto.MaxProofBytes + 1)]
    public void RegisterProof_WithAnInvalidNameOrSize_IsRefused(string? name, long bytes)
    {
        var gallery = NewGallery();

        Should.Throw<DomainException>(() => gallery.RegisterProof(Guid.CreateVersion7(), name!, bytes, Now))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidPhoto);

        gallery.Photos.ShouldBeEmpty();
    }

    /// <summary>
    /// The longest name and the biggest proof allowed are accepted, one more is not.
    /// </summary>
    [Fact]
    public void RegisterProof_AcceptsTheLimitsAndNotOneMore()
    {
        var gallery = NewGallery();

        gallery.RegisterProof(Guid.CreateVersion7(), new string('a', GalleryPhoto.MaxNameLength), GalleryPhoto.MaxProofBytes, Now).ShouldBeTrue();
        Should.Throw<DomainException>(() => gallery.RegisterProof(Guid.CreateVersion7(), new string('a', GalleryPhoto.MaxNameLength + 1), 10, Now));
    }

    /// <summary>
    /// Without an identifier a photo cannot be registered.
    /// </summary>
    [Fact]
    public void RegisterProof_WithoutAnIdentifier_IsRefused() =>
        Should.Throw<DomainException>(() => NewGallery().RegisterProof(Guid.Empty, "a.jpg", 10, Now)).Code.ShouldBe(DomainErrorCodes.RequiredValue);

    /// <summary>
    /// A gallery holds 150 proofs and not one more.
    /// </summary>
    [Fact]
    public void RegisterProof_BeyondTheMaximum_IsRefused()
    {
        var gallery = NewGallery();
        for (var index = 0; index < Gallery.MaxPhotos; index++)
        {
            gallery.RegisterProof(Guid.CreateVersion7(), $"{index}.jpg", 10, Now);
        }

        Should.Throw<DomainException>(() => gallery.RegisterProof(Guid.CreateVersion7(), "extra.jpg", 10, Now)).Code.ShouldBe(DomainErrorCodes.GalleryFull);

        gallery.Photos.Count.ShouldBe(Gallery.MaxPhotos);
        gallery.GetAllowedActions().ShouldNotContain(GalleryAction.UploadProofs);
    }

    /// <summary>
    /// Once the selection is open the proofs are frozen: nothing can be registered or removed.
    /// </summary>
    /// <param name="status">A state after the proofs.</param>
    [Theory]
    [InlineData(GalleryStatus.InSelection)]
    [InlineData(GalleryStatus.InEditing)]
    [InlineData(GalleryStatus.Delivered)]
    public void TheProofs_AreFrozenOnceTheSelectionIsOpen(GalleryStatus status)
    {
        var gallery = NewGallery(status, Guid.CreateVersion7());

        Should.Throw<DomainException>(() => gallery.RegisterProof(Guid.CreateVersion7(), "a.jpg", 10, Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
        Should.Throw<DomainException>(() => gallery.RemoveProof(gallery.Photos[0].Id, Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
        gallery.Photos.Count.ShouldBe(1);
    }

    /// <summary>
    /// A proof can be removed, and removing one that is not there says so without changing anything.
    /// </summary>
    [Fact]
    public void RemoveProof_RemovesIt_AndSaysWhenThereIsNone()
    {
        var gallery = NewGallery();
        var id = Guid.CreateVersion7();
        gallery.RegisterProof(id, "a.jpg", 10, Now);

        gallery.RemoveProof(id, Now.AddMinutes(1)).ShouldBeTrue();
        gallery.UpdatedAt.ShouldBe(Now.AddMinutes(1));
        gallery.RemoveProof(id, Now.AddMinutes(2)).ShouldBeFalse();

        gallery.Photos.ShouldBeEmpty();
        gallery.UpdatedAt.ShouldBe(Now.AddMinutes(1));
    }

    /// <summary>
    /// Opening the selection moves the gallery to selection and raises the event that tells the client.
    /// </summary>
    [Fact]
    public void OpenSelection_MovesToSelection_AndRaisesTheEvent()
    {
        var gallery = NewGallery();
        gallery.RegisterProof(Guid.CreateVersion7(), "a.jpg", 10, Now);

        gallery.OpenSelection(Now.AddHours(1));

        gallery.Status.ShouldBe(GalleryStatus.InSelection);
        gallery.UpdatedAt.ShouldBe(Now.AddHours(1));
        gallery.DomainEvents.ShouldBe([new SelectionOpened(gallery.Id, gallery.BookingId, gallery.PhotographerId, Now.AddHours(1))]);
    }

    /// <summary>
    /// A gallery without proofs cannot open the selection, and neither can one that is not waiting for them.
    /// </summary>
    [Fact]
    public void OpenSelection_WithoutProofs_OrTwice_IsRefused()
    {
        var empty = NewGallery();
        Should.Throw<DomainException>(() => empty.OpenSelection(Now)).Code.ShouldBe(DomainErrorCodes.GalleryNoPhotos);
        empty.Status.ShouldBe(GalleryStatus.Pending);
        empty.DomainEvents.ShouldBeEmpty();

        var open = NewGallery(GalleryStatus.InSelection, Guid.CreateVersion7());
        Should.Throw<DomainException>(() => open.OpenSelection(Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
    }

    /// <summary>
    /// The client is recorded as told once; the second time says it already was.
    /// </summary>
    [Fact]
    public void MarkClientNotified_IsRecordedOnce()
    {
        var gallery = NewGallery(GalleryStatus.InSelection, Guid.CreateVersion7());

        gallery.MarkClientNotified(Now.AddMinutes(5)).ShouldBeTrue();
        gallery.MarkClientNotified(Now.AddMinutes(9)).ShouldBeFalse();

        gallery.SelectionNotifiedAt.ShouldBe(Now.AddMinutes(5));
    }

    /// <summary>
    /// What the photographer can do follows the proofs: upload while there is room, and remove or open only with proofs.
    /// </summary>
    [Fact]
    public void GetAllowedActions_FollowsTheProofs()
    {
        var gallery = NewGallery();
        gallery.GetAllowedActions().ShouldBe([GalleryAction.EditTerms, GalleryAction.UploadProofs]);

        gallery.RegisterProof(Guid.CreateVersion7(), "a.jpg", 10, Now);
        gallery.GetAllowedActions().ShouldBe([GalleryAction.EditTerms, GalleryAction.UploadProofs, GalleryAction.RemoveProof, GalleryAction.OpenSelection]);

        gallery.OpenSelection(Now);
        gallery.GetAllowedActions().ShouldBe([GalleryAction.EditTerms]);
    }

    /// <summary>
    /// A restored gallery keeps its proofs and the instant the client was told.
    /// </summary>
    [Fact]
    public void Restore_KeepsTheProofs()
    {
        var photo = new GalleryPhoto(Guid.CreateVersion7(), "a.jpg", 10, Now);

        var gallery = Gallery.Restore(
            Guid.CreateVersion7(), 3, Guid.CreateVersion7(), Guid.CreateVersion7(), new GallerySnapshot("A", "B", Now), GalleryStatus.InSelection, 15, 0, Money.Create(3000m, "CRC"), Now, Now, [photo], Now.AddMinutes(1));

        gallery.Photos.ShouldBe([photo]);
        gallery.SelectionNotifiedAt.ShouldBe(Now.AddMinutes(1));
    }

    /// <summary>
    /// Builds a gallery with no proofs, or with one, in a given state.
    /// </summary>
    /// <param name="status">The state.</param>
    /// <param name="photoId">The identifier of the proof it already holds, if any.</param>
    /// <returns>The gallery.</returns>
    private static Gallery NewGallery(GalleryStatus status = GalleryStatus.Pending, Guid? photoId = null) => Gallery.Restore(
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
        photoId is null ? [] : [new GalleryPhoto(photoId.Value, "a.jpg", 10, Now)]);
}
