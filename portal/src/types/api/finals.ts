import type { ProofUploadSlot } from './proofs';

/** A place to upload one final and, optionally, its thumbnail, as the API gives it. */
export interface FinalUploadSlot extends ProofUploadSlot {
  /** Signed address to PUT the thumbnail to, or null when the API gives none. It is a secret that expires: it is never logged. */
  readonly thumbUrl: string | null;
  /** Headers the thumbnail upload must send exactly as given. */
  readonly thumbHeaders: Readonly<Record<string, string>>;
}

/** What the browser read from a final photo before sending it, as the API takes it. Every value may be unknown. */
export interface FinalDetailsInfo {
  /** Identifier of the chosen photo the final belongs to. */
  readonly photoId: string;
  /** Width in pixels, or null when unknown. */
  readonly width: number | null;
  /** Height in pixels, or null when unknown. */
  readonly height: number | null;
  /** Instant the photo was taken (ISO 8601), or null when unknown. */
  readonly capturedAt: string | null;
}

/** The place to upload the ZIP, as the API gives it. */
export interface ZipUploadSlot {
  /** Signed address to send the file to with a PUT. It is a secret that expires: it is never logged. */
  readonly url: string;
  /** Headers that must go with the request. */
  readonly headers: Readonly<Record<string, string>>;
  /** Instant the address stops working (ISO 8601). */
  readonly expiresAt: string;
}

/** An address to download a final, for the photographer. */
export interface FinalDownloadLink {
  /** Name to save the file with. */
  readonly fileName: string;
  /** Signed address; lasts a few minutes. It is a secret: it is never logged. */
  readonly url: string;
}

/** One final photo about to be uploaded, as the API asks for it. */
export interface FinalFileInfo {
  /** Identifier of the chosen photo the final belongs to (the identifier of its proof). */
  readonly photoId: string;
  /** Size of the file in bytes. */
  readonly bytes: number;
}
