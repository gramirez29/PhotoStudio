/** What can be read from the start of a JPEG file. Every value may be unknown. */
export interface JpegInfo {
  /** Width in pixels, as the photo is shown (the orientation of the camera is applied), or null when unknown. */
  readonly width: number | null;
  /** Height in pixels, as the photo is shown, or null when unknown. */
  readonly height: number | null;
  /** Instant the photo was taken (ISO 8601), or null when the file does not say. */
  readonly capturedAt: string | null;
}
