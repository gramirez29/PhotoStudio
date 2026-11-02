using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;
using PhotoStudio.Domain.Galleries.Events;

namespace PhotoStudio.Domain.UnitTests.Galleries;

/// <summary>
/// Tests of the final photos of a <see cref="Gallery"/>: registering them, publishing the package (G6), delivering (G7) and
/// releasing the extras by hand.
/// </summary>
public sealed class GalleryFinalsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The first photos chosen, as many as the package covers, are the package; the rest, in the order chosen, are the extras.
    /// </summary>
    [Fact]
    public void PackageAndExtraPhotos_FollowTheOrderOfTheChoice()
    {
        var gallery = EditingGallery(photos: 6, included: 2, chosen: 4);

        gallery.PackagePhotoIds.ShouldBe([.. gallery.SelectedPhotoIds.Take(2)]);
        gallery.ExtraPhotoIds.ShouldBe([.. gallery.SelectedPhotoIds.Skip(2)]);
        gallery.IsExtra(gallery.SelectedPhotoIds[3]).ShouldBeTrue();
        gallery.IsExtra(gallery.SelectedPhotoIds[0]).ShouldBeFalse();
        gallery.IsExtra(Guid.CreateVersion7()).ShouldBeFalse();
    }

    /// <summary>
    /// Registering a final records its size once; the same size again changes nothing, another size replaces it.
    /// </summary>
    [Fact]
    public void RegisterFinal_RecordsTheSize_AndIgnoresTheSameOneAgain()
    {
        var gallery = EditingGallery(photos: 4, included: 2, chosen: 2);
        var photoId = gallery.SelectedPhotoIds[0];

        gallery.RegisterFinal(photoId, 5_000_000, Now).ShouldBeTrue();
        gallery.RegisterFinal(photoId, 5_000_000, Now.AddMinutes(1)).ShouldBeFalse();
        gallery.RegisterFinal(photoId, 6_000_000, Now.AddMinutes(2)).ShouldBeTrue();

        gallery.Photos.Single(photo => photo.Id == photoId).FinalBytes.ShouldBe(6_000_000);
        gallery.HasFinal(photoId).ShouldBeTrue();
        gallery.HasFinal(gallery.SelectedPhotoIds[1]).ShouldBeFalse();
    }

    /// <summary>
    /// A final needs a photo the client chose and a size between 1 byte and 50 MB, in a gallery that is being edited or delivered.
    /// </summary>
    [Fact]
    public void RegisterFinal_RefusesWhatIsNotValid()
    {
        var gallery = EditingGallery(photos: 4, included: 2, chosen: 2);
        var notChosen = gallery.Photos.First(photo => !gallery.SelectedPhotoIds.Contains(photo.Id)).Id;

        Should.Throw<DomainException>(() => gallery.RegisterFinal(notChosen, 100, Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidPhoto);
        Should.Throw<DomainException>(() => gallery.RegisterFinal(gallery.SelectedPhotoIds[0], 0, Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidPhoto);
        Should.Throw<DomainException>(() => gallery.RegisterFinal(gallery.SelectedPhotoIds[0], GalleryPhoto.MaxFinalBytes + 1, Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidPhoto);
        gallery.RegisterFinal(gallery.SelectedPhotoIds[0], GalleryPhoto.MaxFinalBytes, Now).ShouldBeTrue();

        var selecting = EditingGallery(photos: 2, included: 1, chosen: 1, status: GalleryStatus.InSelection);
        Should.Throw<DomainException>(() => selecting.RegisterFinal(selecting.SelectedPhotoIds[0], 100, Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
    }

    /// <summary>
    /// Publishing needs the final of every photo of the package, and nothing from the extras, because they are agreed on apart.
    /// </summary>
    [Fact]
    public void PublishFinals_NeedsThePackageComplete_AndNothingOfTheExtras()
    {
        var gallery = EditingGallery(photos: 5, included: 2, chosen: 4);
        gallery.RegisterFinal(gallery.SelectedPhotoIds[0], 100, Now);

        gallery.GetAllowedActions().ShouldNotContain(GalleryAction.PublishFinals);
        Should.Throw<DomainException>(() => gallery.PublishFinals(Now)).Code.ShouldBe(DomainErrorCodes.GalleryFinalsIncomplete);

        gallery.RegisterFinal(gallery.SelectedPhotoIds[1], 100, Now);
        gallery.GetAllowedActions().ShouldContain(GalleryAction.PublishFinals);
        gallery.PublishFinals(Now.AddHours(1));

        gallery.Status.ShouldBe(GalleryStatus.Ready);
        gallery.FinalsPublishedAt.ShouldBe(Now.AddHours(1));
        var raised = gallery.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<FinalPhotosReady>();
        raised.PackagePhotos.ShouldBe(2);
        Should.Throw<DomainException>(() => gallery.PublishFinals(Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
    }

    /// <summary>
    /// Once published, the finals of the package are frozen, because the client may already be downloading them; those of the extras
    /// can still be uploaded.
    /// </summary>
    [Fact]
    public void AfterPublishing_OnlyTheExtrasCanGetTheirFinal()
    {
        var gallery = PublishedGallery(extras: 1);
        var package = gallery.PackagePhotoIds[0];
        var extra = gallery.ExtraPhotoIds[0];

        Should.Throw<DomainException>(() => gallery.RegisterFinal(package, 999, Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
        gallery.RegisterFinal(extra, 999, Now).ShouldBeTrue();
        Should.Throw<DomainException>(() => gallery.RemoveFinal(package, Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
        gallery.RemoveFinal(extra, Now).ShouldBeTrue();
        gallery.HasFinal(extra).ShouldBeFalse();
        gallery.RemoveFinal(extra, Now).ShouldBeFalse();
    }

    /// <summary>
    /// Delivering moves a published gallery to delivered and raises the event once; doing it again changes nothing.
    /// </summary>
    [Fact]
    public void Deliver_MovesToDelivered_Once()
    {
        var gallery = PublishedGallery(extras: 0);
        gallery.ClearDomainEvents();

        gallery.Deliver(Now.AddDays(1)).ShouldBeTrue();
        gallery.Deliver(Now.AddDays(2)).ShouldBeFalse();

        gallery.Status.ShouldBe(GalleryStatus.Delivered);
        gallery.DeliveredAt.ShouldBe(Now.AddDays(1));
        gallery.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<GalleryDelivered>();
    }

    /// <summary>
    /// A gallery that is still being edited cannot be delivered.
    /// </summary>
    [Fact]
    public void Deliver_BeforePublishing_IsRefused()
    {
        var gallery = EditingGallery(photos: 3, included: 2, chosen: 2);

        Should.Throw<DomainException>(() => gallery.Deliver(Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
    }

    /// <summary>
    /// The extras are released by hand, once, and only when at least one has its final; before publishing it is refused.
    /// </summary>
    [Fact]
    public void ReleaseExtras_NeedsAFinalOfAnExtra_AndHappensOnce()
    {
        var gallery = PublishedGallery(extras: 2);

        gallery.GetAllowedActions().ShouldNotContain(GalleryAction.ReleaseExtras);
        Should.Throw<DomainException>(() => gallery.ReleaseExtras(Now)).Code.ShouldBe(DomainErrorCodes.GalleryFinalsIncomplete);

        gallery.RegisterFinal(gallery.ExtraPhotoIds[0], 100, Now);
        gallery.GetAllowedActions().ShouldContain(GalleryAction.ReleaseExtras);
        gallery.ReleaseExtras(Now.AddDays(1)).ShouldBeTrue();
        gallery.ReleaseExtras(Now.AddDays(2)).ShouldBeFalse();

        gallery.ExtrasReleasedAt.ShouldBe(Now.AddDays(1));
        gallery.GetAllowedActions().ShouldNotContain(GalleryAction.ReleaseExtras);

        var editing = EditingGallery(photos: 4, included: 1, chosen: 2);
        Should.Throw<DomainException>(() => editing.ReleaseExtras(Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
    }

    /// <summary>
    /// Telling the client about the delivery is recorded once.
    /// </summary>
    [Fact]
    public void MarkDeliveryNotified_RecordsOnce()
    {
        var gallery = PublishedGallery(extras: 0);

        gallery.MarkDeliveryNotified(Now).ShouldBeTrue();
        gallery.MarkDeliveryNotified(Now.AddMinutes(1)).ShouldBeFalse();
        gallery.DeliveryNotifiedAt.ShouldBe(Now);
    }

    /// <summary>
    /// A restored gallery keeps the finals and every instant of the delivery.
    /// </summary>
    [Fact]
    public void Restore_KeepsTheDeliveryData()
    {
        var photo = new GalleryPhoto(Guid.CreateVersion7(), "a.jpg", 10, Now, 777, Now.AddHours(1));

        var gallery = Gallery.Restore(
            Guid.CreateVersion7(), 3, Guid.CreateVersion7(), Guid.CreateVersion7(), new GallerySnapshot("María", "Retrato", Now), GalleryStatus.Delivered,
            1, 0, Money.Create(3_000m, "CRC"), Now, Now, [photo], null, [photo.Id], Now, Now.AddDays(1), Now.AddDays(2), Now.AddDays(3), Now.AddDays(4));

        gallery.Photos[0].FinalBytes.ShouldBe(777);
        gallery.Photos[0].FinalUploadedAt.ShouldBe(Now.AddHours(1));
        gallery.FinalsPublishedAt.ShouldBe(Now.AddDays(1));
        gallery.DeliveredAt.ShouldBe(Now.AddDays(2));
        gallery.DeliveryNotifiedAt.ShouldBe(Now.AddDays(3));
        gallery.ExtrasReleasedAt.ShouldBe(Now.AddDays(4));
    }

    /// <summary>
    /// Builds a gallery whose client already chose photos.
    /// </summary>
    /// <param name="photos">Proofs in the gallery.</param>
    /// <param name="included">Photos the package covers.</param>
    /// <param name="chosen">How many photos the client chose.</param>
    /// <param name="status">State of the gallery.</param>
    /// <returns>The gallery.</returns>
    private static Gallery EditingGallery(int photos, int included, int chosen, GalleryStatus status = GalleryStatus.InEditing)
    {
        var proofs = Enumerable.Range(0, photos).Select(index => new GalleryPhoto(Guid.CreateVersion7(), $"IMG_{index:0000}.jpg", 10, Now)).ToList();
        return Gallery.Restore(
            Guid.CreateVersion7(),
            1,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new GallerySnapshot("María Pérez", "Retrato Familiar", Now.AddDays(-1)),
            status,
            included,
            0,
            Money.Create(3_000m, "CRC"),
            Now,
            Now,
            proofs,
            null,
            proofs.Take(chosen).Select(photo => photo.Id),
            Now);
    }

    /// <summary>
    /// Builds a gallery with its package published and the given number of extras that were chosen.
    /// </summary>
    /// <param name="extras">How many extra photos the client chose.</param>
    /// <returns>The gallery, in the state ready.</returns>
    private static Gallery PublishedGallery(int extras)
    {
        var gallery = EditingGallery(photos: 2 + extras + 1, included: 2, chosen: 2 + extras);
        foreach (var photoId in gallery.PackagePhotoIds)
        {
            gallery.RegisterFinal(photoId, 100, Now);
        }

        gallery.PublishFinals(Now);
        return gallery;
    }
}
