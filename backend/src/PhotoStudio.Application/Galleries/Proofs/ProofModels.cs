namespace PhotoStudio.Application.Galleries.Proofs;

/// <summary>
/// A file the photographer is about to upload: its name, as a label, and how much its proof weighs.
/// </summary>
/// <param name="Name">Name of the file.</param>
/// <param name="Bytes">Size of the proof in bytes.</param>
public sealed record ProofFile(string Name, long Bytes);

/// <summary>
/// A place to upload one proof: the address to send it to and what the request must carry.
/// </summary>
/// <param name="PhotoId">Identifier the photo will have; it comes back when the upload is registered.</param>
/// <param name="Url">Signed address to PUT the proof to; it expires and must not be logged.</param>
/// <param name="Headers">Headers the upload must send exactly as given (the content type is part of the signature).</param>
/// <param name="ExpiresAt">Instant the address stops working (UTC).</param>
public sealed record ProofUploadSlot(Guid PhotoId, Uri Url, IReadOnlyDictionary<string, string> Headers, DateTimeOffset ExpiresAt);

/// <summary>
/// The places to upload the proofs of a request, in the order of its files.
/// </summary>
/// <param name="Slots">One slot per file.</param>
public sealed record ProofUploadSlotsResponse(IReadOnlyList<ProofUploadSlot> Slots);

/// <summary>
/// A proof that was uploaded and has to be registered.
/// </summary>
/// <param name="PhotoId">Identifier of the slot it was uploaded to.</param>
/// <param name="Name">Name of the file, as a label.</param>
public sealed record ProofRegistration(Guid PhotoId, string Name);

/// <summary>
/// A proof that could not be registered, and why.
/// </summary>
/// <param name="PhotoId">Identifier of the photo.</param>
/// <param name="Reason">Stable reason: <c>missing</c>, <c>wrong_type</c>, <c>too_big</c>, <c>empty</c>, <c>invalid_name</c> or <c>gallery_full</c>.</param>
public sealed record ProofRejection(Guid PhotoId, string Reason);

/// <summary>
/// The outcome of registering a batch of proofs.
/// </summary>
/// <param name="Gallery">The gallery after registering what was valid.</param>
/// <param name="Rejected">The proofs that were not registered, each with its reason. An object that was invalid is deleted.</param>
public sealed record RegisterProofsResponse(GalleryResponse Gallery, IReadOnlyList<ProofRejection> Rejected);

/// <summary>
/// A proof the photographer can look at.
/// </summary>
/// <param name="Id">Photo identifier.</param>
/// <param name="Name">Name of the file it came from.</param>
/// <param name="Bytes">Size in bytes.</param>
/// <param name="Url">Signed address to show it; it expires in minutes and must not be logged.</param>
/// <param name="ExpiresAt">Instant the address stops working (UTC).</param>
public sealed record ProofItemResponse(Guid Id, string Name, long Bytes, Uri Url, DateTimeOffset ExpiresAt);

/// <summary>
/// The proofs of a gallery, in the order they were registered.
/// </summary>
/// <param name="Items">The proofs.</param>
public sealed record ProofListResponse(IReadOnlyList<ProofItemResponse> Items);
