/** A file the photographer chose on the phone, with what the picker told about it. */
export interface PickedFile {
  /** Address of the file on the phone (a `file://` or `content://` URI). It is local: it is never sent to the API. */
  readonly uri: string;
  /** Name of the file, as the picker gave it. */
  readonly name: string;
  /** Size in bytes; 0 when the picker did not say. */
  readonly bytes: number;
  /** Media type the picker reported, or null when it reported none. */
  readonly mimeType: string | null;
  /** Width in pixels, or null when unknown. */
  readonly width: number | null;
  /** Height in pixels, or null when unknown. */
  readonly height: number | null;
  /** Instant the photo was taken (ISO 8601), or null when its metadata did not say. */
  readonly capturedAt: string | null;
}

/** Size of an image in pixels. */
export interface ImageSize {
  /** Width in pixels. */
  readonly width: number;
  /** Height in pixels. */
  readonly height: number;
}

/** A rectangle placed on an image. */
export interface PlacedRect extends ImageSize {
  /** Distance from the left edge. */
  readonly x: number;
  /** Distance from the top edge. */
  readonly y: number;
}

/** What came of the files chosen as proofs. */
export interface ProofSelection {
  /** The files that can be uploaded, in the order they were chosen. */
  readonly accepted: readonly PickedFile[];
  /** How many were left out for not being JPEG. */
  readonly notJpeg: number;
  /** How many were left out for being already in the queue. */
  readonly duplicates: number;
  /** How many were left out because the gallery has no room for them. */
  readonly noRoom: number;
}

/** A file chosen as a final, paired with the photo of the selection it belongs to. */
export interface FinalMatch {
  /** The file. */
  readonly file: PickedFile;
  /** Identifier of the chosen photo it is the final of. */
  readonly photoId: string;
  /** Name of the original file of that photo. */
  readonly photoName: string;
}

/** Why a file chosen as a final could not go up. */
export type FinalSkipReason = 'not_jpeg' | 'empty' | 'too_big' | 'no_match' | 'duplicate';

/** A file chosen as a final that was left out. */
export interface FinalSkip {
  /** The file. */
  readonly file: PickedFile;
  /** Why it was left out. */
  readonly reason: FinalSkipReason;
}

/** What came of the files chosen as finals. */
export interface FinalSelection {
  /** The files paired with their photo, in the order they were chosen. */
  readonly matches: readonly FinalMatch[];
  /** The files that were left out. */
  readonly skipped: readonly FinalSkip[];
}
