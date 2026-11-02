using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Galleries.Proofs;
using PhotoStudio.Application.Storage;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.Galleries.Finals;

/// <summary>
/// Command to get the place to upload the ZIP with the photos.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery the ZIP is for.</param>
/// <param name="Bytes">Size of the file in bytes, as declared by the photographer.</param>
public sealed record RequestZipUploadCommand(Guid PhotographerId, Guid GalleryId, long Bytes);

/// <summary>
/// The place to upload the ZIP.
/// </summary>
/// <param name="Url">Signed address to send the file to with a PUT.</param>
/// <param name="Headers">Headers that must go with the request.</param>
/// <param name="ExpiresAt">Instant the address stops working (UTC).</param>
public sealed record ZipUploadSlotResponse(Uri Url, IReadOnlyDictionary<string, string> Headers, DateTimeOffset ExpiresAt);

/// <summary>
/// Command to register the ZIP that was uploaded.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery the ZIP belongs to.</param>
public sealed record RegisterZipCommand(Guid PhotographerId, Guid GalleryId);

/// <summary>
/// Command to delete the ZIP.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery the ZIP belongs to.</param>
public sealed record RemoveZipCommand(Guid PhotographerId, Guid GalleryId);

/// <summary>
/// Query for the address to download one final photo, for the photographer.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery the photo belongs to.</param>
/// <param name="PhotoId">Identifier of the chosen photo.</param>
public sealed record GetFinalDownloadQuery(Guid PhotographerId, Guid GalleryId, Guid PhotoId);

/// <summary>
/// An address to download a final photo.
/// </summary>
/// <param name="FileName">Name to save the file with.</param>
/// <param name="Url">Signed address to download it; lasts a few minutes.</param>
/// <param name="ExpiresAt">Instant the address stops working (UTC).</param>
public sealed record FinalDownloadResponse(string FileName, Uri Url, DateTimeOffset ExpiresAt);

/// <summary>
/// Gives the photographer the signed address to upload the ZIP with the photos, which the photographer made by hand. The server
/// never makes a ZIP: it only stores the one it is given. The size declared is checked here and again against the real object when
/// it is registered, because a signed address cannot limit the size of what is sent to it.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="storage">Object storage.</param>
public sealed class RequestZipUploadHandler(IGalleryRepository galleries, IObjectStorage storage)
    : ICommandHandler<RequestZipUploadCommand, ZipUploadSlotResponse>
{
    /// <summary>
    /// Creates the slot.
    /// </summary>
    /// <param name="command">The file.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The place to upload to.</returns>
    /// <exception cref="NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    /// <exception cref="DomainException">When the gallery does not take a ZIP yet, or the size is not valid.</exception>
    public async Task<ZipUploadSlotResponse> HandleAsync(RequestZipUploadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gallery = await galleries.GetOwnedAsync(command.GalleryId, command.PhotographerId, cancellationToken);
        if (!gallery.GetAllowedActions().Contains(GalleryAction.UploadZip))
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"A gallery in state {gallery.Status} does not take a ZIP.");
        }

        if (command.Bytes is < 1 or > Gallery.MaxZipBytes)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidZip, $"A ZIP must weigh between 1 byte and {Gallery.MaxZipBytes} bytes.");
        }

        var url = await storage.CreateUploadUrlAsync(GalleryKeys.Zip(gallery.Id), GalleryKeys.ZipContentType, ObjectStorageLimits.MaxUploadLifetime, cancellationToken);
        return new ZipUploadSlotResponse(url.Url, url.RequiredHeaders, url.ExpiresAt);
    }
}

/// <summary>
/// Registers the ZIP that was uploaded, after looking at what is really in the storage: the object must exist, declare the content
/// type of a ZIP and weigh between 1 byte and the limit. An object that fails is deleted, so nothing the photographer did not mean to
/// publish, and nothing too big, stays; the rejection is an error because there is only one file to register.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="storage">Object storage.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class RegisterZipHandler(IGalleryRepository galleries, IObjectStorage storage, TimeProvider timeProvider)
    : ICommandHandler<RegisterZipCommand, GalleryResponse>
{
    /// <summary>
    /// Registers the ZIP.
    /// </summary>
    /// <param name="command">The gallery.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery with its ZIP.</returns>
    /// <exception cref="NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    /// <exception cref="DomainException">When the upload is missing, is not a ZIP or weighs too much.</exception>
    public async Task<GalleryResponse> HandleAsync(RegisterZipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gallery = await galleries.GetOwnedAsync(command.GalleryId, command.PhotographerId, cancellationToken);
        var key = GalleryKeys.Zip(gallery.Id);
        var info = await storage.GetInfoAsync(key, cancellationToken)
            ?? throw new DomainException(DomainErrorCodes.GalleryInvalidZip, "The ZIP did not arrive at the storage.");
        if (!string.Equals(info.ContentType, GalleryKeys.ZipContentType, StringComparison.OrdinalIgnoreCase) || info.Size is < 1 or > Gallery.MaxZipBytes)
        {
            // A bad file that replaced the previous ZIP leaves nothing to point to: the old one is forgotten with it.
            await storage.DeleteAsync(key, cancellationToken);
            if (gallery.RemoveZip(timeProvider.GetUtcNow()))
            {
                await galleries.UpdateAsync(gallery, cancellationToken);
            }

            throw new DomainException(DomainErrorCodes.GalleryInvalidZip, "The file is not a ZIP of an allowed size.");
        }

        if (gallery.RegisterZip(info.Size, timeProvider.GetUtcNow()))
        {
            await galleries.UpdateAsync(gallery, cancellationToken);
        }

        return gallery.ToResponse();
    }
}

/// <summary>
/// Deletes the ZIP, from the gallery and from the storage, so the photographer can upload another when the client asks again.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="storage">Object storage.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class RemoveZipHandler(IGalleryRepository galleries, IObjectStorage storage, TimeProvider timeProvider)
    : ICommandHandler<RemoveZipCommand, GalleryResponse>
{
    /// <summary>
    /// Deletes the ZIP.
    /// </summary>
    /// <param name="command">The gallery.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery without its ZIP.</returns>
    /// <exception cref="NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    public async Task<GalleryResponse> HandleAsync(RemoveZipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gallery = await galleries.GetOwnedAsync(command.GalleryId, command.PhotographerId, cancellationToken);
        if (gallery.RemoveZip(timeProvider.GetUtcNow()))
        {
            // The record goes first: a file that stays in the storage after a failure is harmless, a record without a file is not.
            await galleries.UpdateAsync(gallery, cancellationToken);
            await storage.DeleteAsync(GalleryKeys.Zip(gallery.Id), cancellationToken);
        }

        return gallery.ToResponse();
    }
}

/// <summary>
/// Gives the photographer the address to download one of the final photos of a gallery, for example to check it. It is not counted as
/// a download of the client.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="storage">Object storage.</param>
public sealed class GetFinalDownloadHandler(IGalleryRepository galleries, IObjectStorage storage)
    : IQueryHandler<GetFinalDownloadQuery, FinalDownloadResponse>
{
    /// <summary>
    /// Creates the address.
    /// </summary>
    /// <param name="query">The photo.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The address.</returns>
    /// <exception cref="NotFoundException">When the gallery does not exist, is not the photographer's, or the photo has no final.</exception>
    public async Task<FinalDownloadResponse> HandleAsync(GetFinalDownloadQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gallery = await galleries.GetOwnedAsync(query.GalleryId, query.PhotographerId, cancellationToken);
        var photo = gallery.Photos.FirstOrDefault(candidate => candidate.Id == query.PhotoId && candidate.HasFinal)
            ?? throw new NotFoundException(nameof(GalleryPhoto), query.PhotoId);
        var fileName = Path.GetFileNameWithoutExtension(photo.OriginalName) + ".jpg";
        var url = await storage.CreateDownloadUrlAsync(GalleryKeys.Final(gallery.Id, photo.Id), TimeSpan.FromMinutes(5), fileName, cancellationToken);
        return new FinalDownloadResponse(fileName, url.Url, url.ExpiresAt);
    }
}
