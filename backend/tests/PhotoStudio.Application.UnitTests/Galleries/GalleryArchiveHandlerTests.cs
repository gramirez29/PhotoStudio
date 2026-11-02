using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Galleries;
using PhotoStudio.Application.Galleries.Archive;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.UnitTests.Galleries;

/// <summary>
/// Tests of freeing the space of a gallery: the release the photographer asks for and the archiving the system does when the
/// retention ends. The files go first and the record after.
/// </summary>
public sealed class GalleryArchiveHandlerTests
{
    private static readonly DateTimeOffset Now = new(2027, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IGalleryRepository _galleries = Substitute.For<IGalleryRepository>();
    private readonly IObjectStorage _storage = Substitute.For<IObjectStorage>();
    private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();
    private readonly FixedTimeProvider _clock = new(Now);

    /// <summary>
    /// Releasing deletes the proof, thumbnail and final of every photo and the ZIP, and then saves the gallery as archived.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Release_DeletesEveryFile_ThenArchives()
    {
        var gallery = Make(GalleryStatus.Delivered, deliveredAt: Now.AddDays(-1));
        _galleries.GetByIdAsync(gallery.Id, Arg.Any<CancellationToken>()).Returns(gallery);
        IReadOnlyCollection<string>? deleted = null;
        await _storage.DeleteManyAsync(Arg.Do<IReadOnlyCollection<string>>(keys => deleted = keys), Arg.Any<CancellationToken>());

        var response = await new ReleaseGalleryHandler(_galleries, new GalleryArchiver(_galleries, _storage), _clock)
            .HandleAsync(new ReleaseGalleryCommand(gallery.PhotographerId, gallery.Id), TestContext.Current.CancellationToken);

        response.Status.ShouldBe(GalleryStatus.Archived);
        var photo = gallery.Photos[0].Id;
        deleted.ShouldNotBeNull();
        deleted.ShouldBe([GalleryKeys.Zip(gallery.Id), GalleryKeys.Proof(gallery.Id, photo), GalleryKeys.Thumb(gallery.Id, photo), GalleryKeys.Final(gallery.Id, photo)], ignoreOrder: true);
        await _galleries.Received(1).UpdateAsync(gallery, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A gallery that was not delivered is not released, and another photographer's gallery does not exist for the caller.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Release_RefusesWhatWasNotDelivered_AndWhatIsNotTheirs()
    {
        var ready = Make(GalleryStatus.Ready, deliveredAt: null);
        _galleries.GetByIdAsync(ready.Id, Arg.Any<CancellationToken>()).Returns(ready);
        var handler = new ReleaseGalleryHandler(_galleries, new GalleryArchiver(_galleries, _storage), _clock);

        (await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(new ReleaseGalleryCommand(ready.PhotographerId, ready.Id), TestContext.Current.CancellationToken)))
            .Code.ShouldBe(DomainErrorCodes.GalleryInvalidState);
        await Should.ThrowAsync<NotFoundException>(() => handler.HandleAsync(new ReleaseGalleryCommand(Guid.CreateVersion7(), ready.Id), TestContext.Current.CancellationToken));
        await _storage.DidNotReceive().DeleteManyAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When the storage fails the gallery is not saved as archived, so the next pass tries again; the pass counts it as skipped.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheStorageFailing_LeavesTheGalleryDelivered_AndSkipsIt()
    {
        var due = Make(GalleryStatus.Delivered, deliveredAt: Now.AddDays(-Gallery.RetentionDays - 1));
        var ok = Make(GalleryStatus.Delivered, deliveredAt: Now.AddDays(-Gallery.RetentionDays - 2));
        _galleries.ListDeliveredBeforeAsync(Now.AddDays(-Gallery.RetentionDays), 20, Arg.Any<CancellationToken>()).Returns([due, ok]);
        _storage.DeleteManyAsync(Arg.Is<IReadOnlyCollection<string>>(keys => keys.Contains(GalleryKeys.Zip(due.Id))), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new StorageUnavailableException("down")));

        var result = await new ArchiveExpiredGalleriesHandler(_galleries, _notifications, new GalleryArchiver(_galleries, _storage), _clock)
            .HandleAsync(new ArchiveExpiredGalleriesCommand(20), TestContext.Current.CancellationToken);

        result.ShouldBe(new ArchiveExpiredGalleriesResult(1, 1));
        await _galleries.DidNotReceive().UpdateAsync(due, Arg.Any<CancellationToken>());
        await _galleries.Received(1).UpdateAsync(ok, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A gallery delivered 90 days ago makes the photographer get one notice, and the next pass finds it again and adds nothing;
    /// one the system is about to archive is not announced.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheNotice_IsSentOnce_AndNotForWhatIsAboutToBeArchived()
    {
        var old = Make(GalleryStatus.Delivered, deliveredAt: Now.AddDays(-Gallery.ArchiveNoticeDays - 5));
        var veryOld = Make(GalleryStatus.Delivered, deliveredAt: Now.AddDays(-Gallery.RetentionDays - 5));
        _galleries.ListDeliveredBeforeAsync(Now.AddDays(-Gallery.ArchiveNoticeDays), ArchiveExpiredGalleriesHandler.NoticeBatchSize, Arg.Any<CancellationToken>()).Returns([veryOld, old]);
        _galleries.ListDeliveredBeforeAsync(Now.AddDays(-Gallery.RetentionDays), 20, Arg.Any<CancellationToken>()).Returns([]);
        _notifications.TryAddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(true, false);
        var handler = new ArchiveExpiredGalleriesHandler(_galleries, _notifications, new GalleryArchiver(_galleries, _storage), _clock);

        (await handler.HandleAsync(new ArchiveExpiredGalleriesCommand(20), TestContext.Current.CancellationToken)).Notified.ShouldBe(1);
        (await handler.HandleAsync(new ArchiveExpiredGalleriesCommand(20), TestContext.Current.CancellationToken)).Notified.ShouldBe(0);

        await _notifications.Received(2).TryAddAsync(
            Arg.Is<Notification>(notice => notice.Type == NotificationType.GalleryArchiveDue && notice.DedupKey == Notification.GalleryArchiveDueKey(old.Id) && notice.PhotographerId == old.PhotographerId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Builds a gallery with one photo that has a final and a ZIP.
    /// </summary>
    /// <param name="status">State of the gallery.</param>
    /// <param name="deliveredAt">Instant it was delivered, if it was.</param>
    /// <returns>The gallery.</returns>
    private static Gallery Make(GalleryStatus status, DateTimeOffset? deliveredAt)
    {
        var photo = new GalleryPhoto(Guid.CreateVersion7(), "a.jpg", 10, Now, 777, Now);
        return Gallery.Restore(
            Guid.CreateVersion7(), 3, Guid.CreateVersion7(), Guid.CreateVersion7(), new GallerySnapshot("María", "Retrato", Now), status,
            1, 0, Money.Create(3_000m, "CRC"), Now, Now, [photo], null, [photo.Id], Now, Now, deliveredAt, null, null, 1_000L, Now);
    }
}
