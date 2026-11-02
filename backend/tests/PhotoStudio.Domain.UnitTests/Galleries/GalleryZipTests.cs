using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Domain.UnitTests.Galleries;

/// <summary>
/// Tests of what the photographer sees about each final photo (size in pixels, date taken, downloads), the downloads of the client
/// and the ZIP made by hand.
/// </summary>
public sealed class GalleryZipTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The size in pixels and the date taken that the browser read are kept with the final.
    /// </summary>
    [Fact]
    public void RegisterFinal_KeepsWhatTheBrowserRead()
    {
        var gallery = Make(GalleryStatus.InEditing);
        var photoId = gallery.PackagePhotoIds[0];
        var taken = new DateTimeOffset(2026, 10, 17, 21, 30, 0, TimeSpan.Zero);

        gallery.RegisterFinal(photoId, 4_000_000, Now, new FinalDetails(1250, 3000, taken)).ShouldBeTrue();

        var photo = gallery.Photos.Single(candidate => candidate.Id == photoId);
        (photo.FinalWidth, photo.FinalHeight, photo.FinalCapturedAt).ShouldBe((1250, 3000, taken));
    }

    /// <summary>
    /// Details that make no sense are dropped, not refused: a photo is not rejected because its metadata is odd.
    /// </summary>
    [Fact]
    public void RegisterFinal_DropsDetailsThatMakeNoSense()
    {
        var gallery = Make(GalleryStatus.InEditing);
        var photoId = gallery.PackagePhotoIds[0];

        gallery.RegisterFinal(photoId, 100, Now, new FinalDetails(0, 3000, Now.AddYears(5)));

        var photo = gallery.Photos.Single(candidate => candidate.Id == photoId);
        (photo.FinalWidth, photo.FinalHeight, photo.FinalCapturedAt).ShouldBe((null, null, null));
        new FinalDetails(100_001, 10, new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero)).Normalize(Now).ShouldBe(new FinalDetails(null, null, null));
    }

    /// <summary>
    /// Registering the same file again with the same details changes nothing; with other details it updates them but keeps the count.
    /// A file of another size is another file: its downloads start again.
    /// </summary>
    [Fact]
    public void RegisterFinal_TheSameFile_KeepsTheCount_AndAnotherFileStartsAgain()
    {
        var gallery = Make(GalleryStatus.InEditing);
        var photoId = gallery.PackagePhotoIds[0];
        gallery.RegisterFinal(photoId, 100, Now, new FinalDetails(10, 20, null));
        gallery.RegisterFinal(photoId, 100, Now, new FinalDetails(10, 20, null)).ShouldBeFalse();
        gallery.RegisterFinal(photoId, 100, Now, new FinalDetails(30, 40, null)).ShouldBeTrue();
        gallery.Photos.Single(candidate => candidate.Id == photoId).FinalWidth.ShouldBe(30);

        var delivered = Make(GalleryStatus.Delivered);
        var extra = delivered.ExtraPhotoIds[0];
        delivered.RegisterFinal(extra, 100, Now);
        delivered.ReleaseExtras(Now);
        delivered.RecordFinalDownloads([extra], Now);
        delivered.RegisterFinal(extra, 100, Now).ShouldBeFalse();
        delivered.Photos.Single(photo => photo.Id == extra).FinalDownloads.ShouldBe(1);
        delivered.RegisterFinal(extra, 200, Now).ShouldBeTrue();
        delivered.Photos.Single(photo => photo.Id == extra).FinalDownloads.ShouldBe(0);
        Should.Throw<DomainException>(() => delivered.RegisterFinal(delivered.PackagePhotoIds[0], 200, Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
    }

    /// <summary>
    /// Asking for the photos counts one download each, once per request even if a photo is repeated, and only when the gallery is
    /// delivered.
    /// </summary>
    [Fact]
    public void RecordFinalDownloads_CountsOncePerPhotoPerRequest()
    {
        var gallery = Make(GalleryStatus.Delivered);
        var first = gallery.PackagePhotoIds[0];
        var second = gallery.PackagePhotoIds[1];

        gallery.RecordFinalDownloads([first, first, second], Now);
        gallery.RecordFinalDownloads([first], Now.AddMinutes(1));

        gallery.Photos.Single(photo => photo.Id == first).FinalDownloads.ShouldBe(2);
        gallery.Photos.Single(photo => photo.Id == second).FinalDownloads.ShouldBe(1);

        var ready = Make(GalleryStatus.Ready);
        Should.Throw<DomainException>(() => ready.RecordFinalDownloads([ready.PackagePhotoIds[0]], Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
    }

    /// <summary>
    /// A photo that the client may not download makes the whole request fail and counts nothing: an extra that was not released, a
    /// photo without a final, a photo the client did not choose, or no photos at all.
    /// </summary>
    [Fact]
    public void RecordFinalDownloads_RefusesWhatTheClientMayNotTake_AndCountsNothing()
    {
        var gallery = Make(GalleryStatus.Delivered);
        var package = gallery.PackagePhotoIds[0];
        var extra = gallery.ExtraPhotoIds[0];
        gallery.RegisterFinal(extra, 100, Now);
        var notChosen = gallery.Photos.First(photo => !gallery.SelectedPhotoIds.Contains(photo.Id)).Id;

        gallery.IsDownloadable(extra).ShouldBeFalse();
        Should.Throw<DomainException>(() => gallery.RecordFinalDownloads([package, extra], Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidPhoto);
        Should.Throw<DomainException>(() => gallery.RecordFinalDownloads([package, notChosen], Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidPhoto);
        Should.Throw<DomainException>(() => gallery.RecordFinalDownloads([], Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidPhoto);
        gallery.Photos.Single(photo => photo.Id == package).FinalDownloads.ShouldBe(0);

        gallery.ReleaseExtras(Now);
        gallery.IsDownloadable(extra).ShouldBeTrue();
        gallery.RecordFinalDownloads([extra], Now);
        gallery.Photos.Single(photo => photo.Id == extra).FinalDownloads.ShouldBe(1);
    }

    /// <summary>
    /// The ZIP is registered once with its size; the same size again changes nothing, another replaces it and starts the count again.
    /// </summary>
    [Fact]
    public void RegisterZip_RecordsTheSize_AndAnotherOneReplacesIt()
    {
        var gallery = Make(GalleryStatus.Delivered);

        gallery.RegisterZip(300_000_000, Now).ShouldBeTrue();
        gallery.RegisterZip(300_000_000, Now.AddMinutes(1)).ShouldBeFalse();
        gallery.RecordZipDownload(Now.AddHours(1));
        gallery.RegisterZip(310_000_000, Now.AddHours(2)).ShouldBeTrue();

        gallery.ZipBytes.ShouldBe(310_000_000);
        gallery.ZipUploadedAt.ShouldBe(Now.AddHours(2));
        gallery.ZipDownloads.ShouldBe(0);
        gallery.ZipLastDownloadedAt.ShouldBeNull();
        gallery.HasZip.ShouldBeTrue();
    }

    /// <summary>
    /// A ZIP needs the finals to be published and a size between 1 byte and 500 MB.
    /// </summary>
    [Fact]
    public void RegisterZip_RefusesWhatIsNotValid()
    {
        var editing = Make(GalleryStatus.InEditing);
        Should.Throw<DomainException>(() => editing.RegisterZip(100, Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);

        var ready = Make(GalleryStatus.Ready);
        Should.Throw<DomainException>(() => ready.RegisterZip(0, Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidZip);
        Should.Throw<DomainException>(() => ready.RegisterZip(Gallery.MaxZipBytes + 1, Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidZip);
        ready.RegisterZip(Gallery.MaxZipBytes, Now).ShouldBeTrue();
        Gallery.MaxZipBytes.ShouldBe(524_288_000);
    }

    /// <summary>
    /// The client can download the ZIP only when the gallery is delivered and has one; each download is counted with its instant.
    /// </summary>
    [Fact]
    public void RecordZipDownload_CountsWhenThereIsOne()
    {
        var gallery = Make(GalleryStatus.Delivered);
        Should.Throw<DomainException>(() => gallery.RecordZipDownload(Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);

        gallery.RegisterZip(100, Now);
        gallery.RecordZipDownload(Now.AddHours(1));
        gallery.RecordZipDownload(Now.AddHours(2));

        gallery.ZipDownloads.ShouldBe(2);
        gallery.ZipLastDownloadedAt.ShouldBe(Now.AddHours(2));

        var ready = Make(GalleryStatus.Ready);
        ready.RegisterZip(100, Now);
        Should.Throw<DomainException>(() => ready.RecordZipDownload(Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
    }

    /// <summary>
    /// Removing the ZIP forgets it and its count, leaves the photos, and does nothing when there is none.
    /// </summary>
    [Fact]
    public void RemoveZip_ForgetsIt()
    {
        var gallery = Make(GalleryStatus.Delivered);
        gallery.RemoveZip(Now).ShouldBeFalse();
        gallery.RegisterZip(100, Now);
        gallery.RecordZipDownload(Now);

        gallery.RemoveZip(Now.AddMinutes(1)).ShouldBeTrue();

        (gallery.ZipBytes, gallery.ZipUploadedAt, gallery.ZipDownloads, gallery.ZipLastDownloadedAt).ShouldBe((null, null, 0, null));
        gallery.Photos.Count(photo => photo.HasFinal).ShouldBe(2);
    }

    /// <summary>
    /// The actions about the ZIP are there once the finals are published: upload always, remove only with a ZIP.
    /// </summary>
    [Fact]
    public void AllowedActions_OfTheZip()
    {
        var gallery = Make(GalleryStatus.Ready);

        gallery.GetAllowedActions().ShouldContain(GalleryAction.UploadZip);
        gallery.GetAllowedActions().ShouldNotContain(GalleryAction.RemoveZip);
        gallery.RegisterZip(100, Now);
        gallery.GetAllowedActions().ShouldContain(GalleryAction.RemoveZip);
        Make(GalleryStatus.InEditing).GetAllowedActions().ShouldNotContain(GalleryAction.UploadZip);
    }

    /// <summary>
    /// A restored gallery keeps the details of the finals, their downloads and the ZIP.
    /// </summary>
    [Fact]
    public void Restore_KeepsTheZipAndTheDetails()
    {
        var taken = Now.AddDays(-20);
        var photo = new GalleryPhoto(Guid.CreateVersion7(), "a.jpg", 10, Now, 777, Now, 1250, 3000, taken, 4);

        var gallery = Gallery.Restore(
            Guid.CreateVersion7(), 3, Guid.CreateVersion7(), Guid.CreateVersion7(), new GallerySnapshot("María", "Retrato", Now), GalleryStatus.Delivered,
            1, 0, Money.Create(3_000m, "CRC"), Now, Now, [photo], null, [photo.Id], Now, Now, Now, Now, null, 500, Now.AddDays(1), 3, Now.AddDays(2));

        (gallery.Photos[0].FinalWidth, gallery.Photos[0].FinalHeight, gallery.Photos[0].FinalCapturedAt, gallery.Photos[0].FinalDownloads).ShouldBe((1250, 3000, taken, 4));
        (gallery.ZipBytes, gallery.ZipUploadedAt, gallery.ZipDownloads, gallery.ZipLastDownloadedAt).ShouldBe((500, Now.AddDays(1), 3, Now.AddDays(2)));
    }

    /// <summary>
    /// Builds a gallery of 5 proofs where the client chose 3 (2 of the package and 1 extra), with the finals of the package.
    /// </summary>
    /// <param name="status">State of the gallery.</param>
    /// <returns>The gallery.</returns>
    private static Gallery Make(GalleryStatus status)
    {
        var proofs = Enumerable.Range(0, 5).Select(index => new GalleryPhoto(Guid.CreateVersion7(), $"IMG_{index:0000}.jpg", 10, Now)).ToList();
        var chosen = proofs.Take(3).Select(photo => photo.Id).ToList();
        proofs = [.. proofs.Select(photo => chosen.Take(2).Contains(photo.Id) ? photo with { FinalBytes = 100, FinalUploadedAt = Now } : photo)];
        return Gallery.Restore(
            Guid.CreateVersion7(), 1, Guid.CreateVersion7(), Guid.CreateVersion7(), new GallerySnapshot("María Pérez", "Retrato Familiar", Now.AddDays(-1)),
            status, 2, 0, Money.Create(3_000m, "CRC"), Now, Now, proofs, null, chosen, Now);
    }
}
