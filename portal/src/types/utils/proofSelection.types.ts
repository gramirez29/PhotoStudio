/** What came of the files a person chose to upload. */
export interface ProofSelection {
  /** The files that can be uploaded, in the order they were chosen. */
  readonly accepted: readonly File[];
  /** Files that are not JPEG. */
  readonly notJpeg: number;
  /** Files already in the queue (same name, size and date). */
  readonly duplicates: number;
  /** Files left out because the gallery has no room for them. */
  readonly noRoom: number;
}
