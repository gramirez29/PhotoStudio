using PhotoStudio.Application.Storage;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Port to the object storage that holds the photos (Cloudflare R2 in production). The bytes never go through the API: the
/// caller gets a short-lived signed address and talks to the storage directly. The bucket is private; every access goes through
/// one of these addresses.
/// </summary>
public interface IObjectStorage
{
    /// <summary>
    /// Creates a signed address to upload one object with HTTP PUT. The content type is part of the signature: the caller must send exactly it.
    /// </summary>
    /// <param name="key">Key of the object; see <see cref="ObjectKey"/>.</param>
    /// <param name="contentType">Content type the upload must have.</param>
    /// <param name="lifetime">How long the address works; at most <see cref="ObjectStorageLimits.MaxUploadLifetime"/>.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The address and the headers that must go with it.</returns>
    /// <exception cref="ArgumentException">When the key is not valid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When the lifetime is not positive or is longer than allowed.</exception>
    /// <exception cref="Exceptions.StorageUnavailableException">When the storage is not configured.</exception>
    Task<PresignedUrl> CreateUploadUrlAsync(string key, string contentType, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a signed address to download one object with HTTP GET.
    /// </summary>
    /// <param name="key">Key of the object; see <see cref="ObjectKey"/>.</param>
    /// <param name="lifetime">How long the address works; at most <see cref="ObjectStorageLimits.MaxDownloadLifetime"/>.</param>
    /// <param name="downloadFileName">When given, the browser saves the file with this name instead of showing it.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The address.</returns>
    /// <exception cref="ArgumentException">When the key is not valid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When the lifetime is not positive or is longer than allowed.</exception>
    /// <exception cref="Exceptions.StorageUnavailableException">When the storage is not configured.</exception>
    Task<PresignedUrl> CreateDownloadUrlAsync(string key, TimeSpan lifetime, string? downloadFileName, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the size and content type of an object, to check an upload really happened before it is registered.
    /// </summary>
    /// <param name="key">Key of the object.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>What the storage reports, or <see langword="null"/> when the object does not exist.</returns>
    /// <exception cref="ArgumentException">When the key is not valid.</exception>
    /// <exception cref="Exceptions.StorageUnavailableException">When the storage is not configured or did not answer.</exception>
    Task<StoredObjectInfo?> GetInfoAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one object. Deleting one that does not exist is not an error.
    /// </summary>
    /// <param name="key">Key of the object.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the object is gone.</returns>
    /// <exception cref="ArgumentException">When the key is not valid.</exception>
    /// <exception cref="Exceptions.StorageUnavailableException">When the storage is not configured or refused the deletion.</exception>
    Task DeleteAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes several objects, in one call when they fit (<see cref="ObjectStorageLimits.MaxKeysPerDelete"/>) and in more when they do not.
    /// </summary>
    /// <param name="keys">Keys of the objects.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when every object is gone.</returns>
    /// <exception cref="ArgumentException">When a key is not valid; nothing is deleted then.</exception>
    /// <exception cref="Exceptions.StorageUnavailableException">When the storage is not configured or refused some deletion.</exception>
    Task DeleteManyAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken);
}
