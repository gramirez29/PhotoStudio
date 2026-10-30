namespace PhotoStudio.Application.Storage;

/// <summary>
/// A temporary signed address to upload or download one object straight to or from the storage, without the bytes going through the API.
/// </summary>
/// <param name="Url">The signed address. It is a secret until it expires: it must not be logged.</param>
/// <param name="ExpiresAt">Instant after which the storage refuses the address (UTC).</param>
/// <param name="RequiredHeaders">Headers the caller must send exactly as given, because they are part of the signature (for an upload, the content type).</param>
public sealed record PresignedUrl(Uri Url, DateTimeOffset ExpiresAt, IReadOnlyDictionary<string, string> RequiredHeaders);
