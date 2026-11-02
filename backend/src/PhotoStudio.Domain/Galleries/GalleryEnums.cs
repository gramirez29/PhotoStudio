namespace PhotoStudio.Domain.Galleries;

/// <summary>
/// Where a gallery is in its life, from the session to the end of the delivery (`CLAUDE.md` §8.6). Only some of the states
/// are reached by code that exists today; the rest come with the pull requests that build their transitions.
/// </summary>
public enum GalleryStatus
{
    /// <summary>Created when the session was completed; the photographer has not opened the selection yet.</summary>
    Pending,

    /// <summary>The proofs are published and the client is choosing photos.</summary>
    InSelection,

    /// <summary>The client sent the selection and the photographer is editing those photos.</summary>
    InEditing,

    /// <summary>The final photos are published.</summary>
    Ready,

    /// <summary>The balance is zero and the client can download the final photos.</summary>
    Delivered,

    /// <summary>The photos were removed from the storage; only the record remains.</summary>
    Archived,
}

/// <summary>
/// What the photographer can do with a gallery right now; the API computes it from the state so the clients never decide it.
/// </summary>
public enum GalleryAction
{
    /// <summary>Change how many photos are included, how many are a gift and the price of an extra photo.</summary>
    EditTerms,

    /// <summary>Upload more proofs: the gallery is waiting for them and has room.</summary>
    UploadProofs,

    /// <summary>Remove a proof that was uploaded by mistake.</summary>
    RemoveProof,

    /// <summary>Publish the proofs so the client can choose.</summary>
    OpenSelection,

    /// <summary>Let the client choose again: the photographer reopens the selection they sent, with a reason.</summary>
    ReopenSelection,

    /// <summary>Upload the final photos of the selection (after publishing, only those of the extra photos).</summary>
    UploadFinals,

    /// <summary>Publish the final photos of the package (G6).</summary>
    PublishFinals,

    /// <summary>Upload the ZIP with the photos, made by hand by the photographer.</summary>
    UploadZip,

    /// <summary>Delete the ZIP, for example to upload a new one.</summary>
    RemoveZip,

    /// <summary>Let the client download the extra photos, once the client agreed on their payment with the studio.</summary>
    ReleaseExtras,

    /// <summary>Free the space: delete every photo and the ZIP from the storage and archive the gallery. It cannot be undone.</summary>
    Release,
}
