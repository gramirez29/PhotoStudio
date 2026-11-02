using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Galleries.Proofs;
using PhotoStudio.Application.Storage;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.Galleries.Archive;

/// <summary>
/// Command to free the space of a gallery: delete its files and archive it.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery to release.</param>
public sealed record ReleaseGalleryCommand(Guid PhotographerId, Guid GalleryId);

/// <summary>
/// Command to archive the delivered galleries whose retention ended.
/// </summary>
/// <param name="BatchSize">Most galleries to look at.</param>
public sealed record ArchiveExpiredGalleriesCommand(int BatchSize);

/// <summary>
/// What a pass of archiving did.
/// </summary>
/// <param name="Archived">Galleries archived.</param>
/// <param name="Skipped">Galleries that could not be archived now (the storage failed or another request changed them); the next pass tries again.</param>
/// <param name="Notified">Galleries whose photographer was told, for the first time, that they can be released.</param>
public sealed record ArchiveExpiredGalleriesResult(int Archived, int Skipped, int Notified = 0);

/// <summary>
/// Deletes the files of a gallery and archives it. The files go first and the record after: if the storage fails nothing changes and
/// the gallery is tried again, and if the record cannot be saved the gallery is archived on the next try (deleting what is gone is not
/// an error). The reverse would leave a gallery marked as archived whose files still take space.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="storage">Object storage.</param>
public sealed class GalleryArchiver(IGalleryRepository galleries, IObjectStorage storage)
{
    /// <summary>
    /// Archives a delivered gallery.
    /// </summary>
    /// <param name="gallery">The gallery.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the files are gone and the gallery is archived.</returns>
    /// <exception cref="DomainException">When the gallery is not delivered.</exception>
    public async Task ArchiveAsync(Gallery gallery, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gallery);

        if (!gallery.Archive(now))
        {
            return;
        }

        var keys = new List<string> { GalleryKeys.Zip(gallery.Id) };
        foreach (var photo in gallery.Photos)
        {
            keys.Add(GalleryKeys.Proof(gallery.Id, photo.Id));
            keys.Add(GalleryKeys.Thumb(gallery.Id, photo.Id));
            keys.Add(GalleryKeys.Final(gallery.Id, photo.Id));
        }

        await storage.DeleteManyAsync(keys, cancellationToken);
        await galleries.UpdateAsync(gallery, cancellationToken);
    }
}

/// <summary>
/// Releases a gallery at the request of the photographer: the same as the retention does, before its term.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="archiver">Deletes the files and archives.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class ReleaseGalleryHandler(IGalleryRepository galleries, GalleryArchiver archiver, TimeProvider timeProvider)
    : ICommandHandler<ReleaseGalleryCommand, GalleryResponse>
{
    /// <summary>
    /// Releases the gallery.
    /// </summary>
    /// <param name="command">The gallery.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery, archived.</returns>
    /// <exception cref="NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    /// <exception cref="DomainException">When the gallery was not delivered (409 <c>gallery.invalid_state</c>).</exception>
    public async Task<GalleryResponse> HandleAsync(ReleaseGalleryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gallery = await galleries.GetOwnedAsync(command.GalleryId, command.PhotographerId, cancellationToken);
        await archiver.ArchiveAsync(gallery, timeProvider.GetUtcNow(), cancellationToken);
        return gallery.ToResponse();
    }
}

/// <summary>
/// Looks after the galleries that were delivered long ago (G8), in the maintenance pass. After <see cref="Gallery.ArchiveNoticeDays"/>
/// days the photographer is told, once, that the gallery can be released and decides; after <see cref="Gallery.RetentionDays"/> days
/// the system archives it as a last resort. A gallery that fails is skipped and tried again in the next pass.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="notifications">Notification repository.</param>
/// <param name="archiver">Deletes the files and archives.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class ArchiveExpiredGalleriesHandler(IGalleryRepository galleries, INotificationRepository notifications, GalleryArchiver archiver, TimeProvider timeProvider)
    : ICommandHandler<ArchiveExpiredGalleriesCommand, ArchiveExpiredGalleriesResult>
{
    /// <summary>
    /// Most galleries looked at for notices in a pass.
    /// </summary>
    public const int NoticeBatchSize = 200;

    /// <summary>
    /// Notifies and archives a batch.
    /// </summary>
    /// <param name="command">The batch size.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>How many were archived and how many were skipped.</returns>
    public async Task<ArchiveExpiredGalleriesResult> HandleAsync(ArchiveExpiredGalleriesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var notified = await NotifyAsync(now, cancellationToken);
        var due = await galleries.ListDeliveredBeforeAsync(now.AddDays(-Gallery.RetentionDays), command.BatchSize, cancellationToken);
        var archived = 0;
        var skipped = 0;
        foreach (var gallery in due)
        {
            try
            {
                await archiver.ArchiveAsync(gallery, now, cancellationToken);
                archived++;
            }
            catch (Exception exception) when (exception is StorageUnavailableException or ConflictException or DomainException)
            {
                skipped++;
            }
        }

        return new ArchiveExpiredGalleriesResult(archived, skipped, notified);
    }

    /// <summary>
    /// Tells the photographers about the galleries that can be released. The notice is deduplicated by gallery, so a gallery the
    /// photographer leaves alone is not announced again in the next pass. The galleries the system is about to archive are not announced.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>How many notices were created now.</returns>
    private async Task<int> NotifyAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var candidates = await galleries.ListDeliveredBeforeAsync(now.AddDays(-Gallery.ArchiveNoticeDays), NoticeBatchSize, cancellationToken);
        var notified = 0;
        foreach (var gallery in candidates.Where(gallery => !gallery.IsRetentionOver(now)))
        {
            var added = await notifications.TryAddAsync(
                Notification.Schedule(gallery.PhotographerId, gallery.BookingId, NotificationType.GalleryArchiveDue, Notification.GalleryArchiveDueKey(gallery.Id), now, now),
                cancellationToken);
            if (added)
            {
                notified++;
            }
        }

        return notified;
    }
}
