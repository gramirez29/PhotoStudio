using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Storage;

namespace PhotoStudio.Infrastructure.Storage;

/// <summary>
/// Stands in for the object storage when it is not configured: the process starts, and anything that needs the storage fails
/// with <see cref="StorageUnavailableException"/> (503) instead of the process refusing to start.
/// </summary>
public sealed class UnavailableObjectStorage : IObjectStorage
{
    private const string Reason = "The object storage is not configured.";

    /// <inheritdoc />
    public Task<PresignedUrl> CreateUploadUrlAsync(string key, string contentType, TimeSpan lifetime, CancellationToken cancellationToken) =>
        Task.FromException<PresignedUrl>(new StorageUnavailableException(Reason));

    /// <inheritdoc />
    public Task<PresignedUrl> CreateDownloadUrlAsync(string key, TimeSpan lifetime, string? downloadFileName, CancellationToken cancellationToken) =>
        Task.FromException<PresignedUrl>(new StorageUnavailableException(Reason));

    /// <inheritdoc />
    public Task<StoredObjectInfo?> GetInfoAsync(string key, CancellationToken cancellationToken) =>
        Task.FromException<StoredObjectInfo?>(new StorageUnavailableException(Reason));

    /// <inheritdoc />
    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        Task.FromException(new StorageUnavailableException(Reason));

    /// <inheritdoc />
    public Task DeleteManyAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken) =>
        Task.FromException(new StorageUnavailableException(Reason));
}
