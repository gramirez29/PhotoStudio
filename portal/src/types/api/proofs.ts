import type { StudioGallery } from './studio';

/** A file about to be uploaded, as the API is told about it. */
export interface ProofFileInfo {
  /** Name of the original file, kept as a label. */
  readonly name: string;
  /** Size of the proof in bytes. */
  readonly bytes: number;
}

/** A place to upload one proof, given by the API. */
export interface ProofUploadSlot {
  /** Identifier the photo will have; it goes back when the upload is registered. */
  readonly photoId: string;
  /** Signed address to PUT the proof to. It is a secret that expires: it is never logged or shown. */
  readonly url: string;
  /** Headers the upload must send exactly as given (the content type is part of the signature). */
  readonly headers: Readonly<Record<string, string>>;
  /** Instant the address stops working, ISO 8601. */
  readonly expiresAt: string;
}

/** An upload that finished and has to be registered. */
export interface ProofRegistration {
  /** Identifier of the slot it was uploaded to. */
  readonly photoId: string;
  /** Name of the original file, kept as a label. */
  readonly name: string;
}

/** A proof the API did not register, and why. */
export interface ProofRejection {
  /** Identifier of the photo. */
  readonly photoId: string;
  /** Stable reason: `missing`, `wrong_type`, `too_big`, `empty`, `invalid_name` or `gallery_full` (others may come later). */
  readonly reason: string;
}

/** What registering a batch of proofs gave back. */
export interface RegisterProofsResult {
  /** The gallery after registering what was valid. */
  readonly gallery: StudioGallery;
  /** The proofs that were not registered. */
  readonly rejected: readonly ProofRejection[];
}

/** A proof of a gallery, with a signed address to look at it. */
export interface StudioProof {
  /** Photo identifier. */
  readonly id: string;
  /** Name of the original file. */
  readonly name: string;
  /** Size in bytes. */
  readonly bytes: number;
  /** Signed address to show it; it expires in minutes and is never logged. */
  readonly url: string;
  /** Instant the address stops working, ISO 8601. */
  readonly expiresAt: string;
}
