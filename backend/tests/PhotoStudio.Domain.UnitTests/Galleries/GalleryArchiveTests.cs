using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Domain.UnitTests.Galleries;

/// <summary>
/// Tests of archiving a <see cref="Gallery"/> (G8): by the photographer before the term and by the system when the retention ends.
/// </summary>
public sealed class GalleryArchiveTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A delivered gallery is archived once, forgets its ZIP and offers nothing more to do.
    /// </summary>
    [Fact]
    public void Archive_ArchivesADeliveredGallery_Once()
    {
        var gallery = Make(GalleryStatus.Delivered, withZip: true);
        gallery.GetAllowedActions().ShouldContain(GalleryAction.Release);

        gallery.Archive(Now.AddDays(5)).ShouldBeTrue();
        gallery.Archive(Now.AddDays(6)).ShouldBeFalse();

        gallery.Status.ShouldBe(GalleryStatus.Archived);
        gallery.HasZip.ShouldBeFalse();
        gallery.GetAllowedActions().ShouldBeEmpty();
    }

    /// <summary>
    /// A gallery that was not delivered cannot be archived: the client has not downloaded anything yet. Release is not offered either.
    /// </summary>
    [Theory]
    [InlineData(GalleryStatus.Pending)]
    [InlineData(GalleryStatus.InSelection)]
    [InlineData(GalleryStatus.InEditing)]
    [InlineData(GalleryStatus.Ready)]
    public void Archive_RefusesWhatWasNotDelivered(GalleryStatus status)
    {
        var gallery = Make(status);

        Should.Throw<DomainException>(() => gallery.Archive(Now)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
        gallery.GetAllowedActions().ShouldNotContain(GalleryAction.Release);
    }

    /// <summary>
    /// The notice term (90 days) and the retention (180 days) are counted from the delivery, and only for a delivered gallery.
    /// </summary>
    [Fact]
    public void IsRetentionOver_CountsFromTheDelivery()
    {
        var delivered = Make(GalleryStatus.Delivered);

        delivered.IsArchiveNoticeDue(Now.AddDays(Gallery.ArchiveNoticeDays).AddSeconds(-1)).ShouldBeFalse();
        delivered.IsArchiveNoticeDue(Now.AddDays(Gallery.ArchiveNoticeDays)).ShouldBeTrue();
        Make(GalleryStatus.Ready).IsArchiveNoticeDue(Now.AddDays(365)).ShouldBeFalse();
        delivered.IsRetentionOver(Now.AddDays(Gallery.RetentionDays).AddSeconds(-1)).ShouldBeFalse();
        delivered.IsRetentionOver(Now.AddDays(Gallery.RetentionDays)).ShouldBeTrue();
        Make(GalleryStatus.Ready).IsRetentionOver(Now.AddDays(365)).ShouldBeFalse();
    }

    /// <summary>
    /// Builds a gallery in a state, delivered at <c>Now</c> when it is delivered.
    /// </summary>
    /// <param name="status">State of the gallery.</param>
    /// <param name="withZip">Whether it has a ZIP.</param>
    /// <returns>The gallery.</returns>
    private static Gallery Make(GalleryStatus status, bool withZip = false)
    {
        var photo = new GalleryPhoto(Guid.CreateVersion7(), "a.jpg", 10, Now, 777, Now);
        var delivered = status == GalleryStatus.Delivered;
        return Gallery.Restore(
            Guid.CreateVersion7(), 3, Guid.CreateVersion7(), Guid.CreateVersion7(), new GallerySnapshot("María", "Retrato", Now), status,
            1, 0, Money.Create(3_000m, "CRC"), Now, Now, [photo], null, [photo.Id], Now, Now, delivered ? Now : null, null, null,
            withZip ? 1_000L : null, withZip ? Now : null);
    }
}
