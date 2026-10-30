namespace PhotoStudio.Application.Storage;

/// <summary>
/// What the storage reports about an object that exists, without reading it.
/// </summary>
/// <param name="Size">Size in bytes.</param>
/// <param name="ContentType">Content type the object was stored with.</param>
public sealed record StoredObjectInfo(long Size, string ContentType);
