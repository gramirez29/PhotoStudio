namespace PhotoStudio.Application.Storage;

/// <summary>
/// Bounds on how long the addresses handed out by <see cref="Abstractions.IObjectStorage"/> work. They are short on purpose: an
/// address that was already issued cannot be taken back before it expires, so its life is the window in which a leaked one is useful.
/// </summary>
public static class ObjectStorageLimits
{
    /// <summary>
    /// The longest an upload address can live.
    /// </summary>
    public static readonly TimeSpan MaxUploadLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The longest a download address can live.
    /// </summary>
    public static readonly TimeSpan MaxDownloadLifetime = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The most keys a single call to delete many objects accepts; callers with more split the work.
    /// </summary>
    public const int MaxKeysPerDelete = 1000;
}
