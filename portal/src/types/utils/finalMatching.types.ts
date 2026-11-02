/** A file of the photographer paired with the photo of the selection it is the final of. */
export interface FinalMatch {
  /** The file the photographer chose. */
  readonly file: File;
  /** Identifier of the chosen photo the final belongs to. */
  readonly photoId: string;
  /** Name of the original file of that photo, as the client's selection lists it. */
  readonly photoName: string;
}

/** Why a file cannot be taken as a final. */
export type FinalSkipReason = 'not_jpeg' | 'too_big' | 'empty' | 'no_match' | 'duplicate';

/** A file that was left out, and why. */
export interface FinalSkip {
  /** The file the photographer chose. */
  readonly file: File;
  /** Why it was left out. */
  readonly reason: FinalSkipReason;
}

/** What came of the files the photographer chose: which can go up, and which cannot. */
export interface FinalSelection {
  /** The files paired with a photo of the selection, in the order they were chosen. */
  readonly matches: readonly FinalMatch[];
  /** The files that were left out. */
  readonly skipped: readonly FinalSkip[];
}
