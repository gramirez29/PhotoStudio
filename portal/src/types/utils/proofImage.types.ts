/** Width and height of an image, in pixels. */
export interface ImageSize {
  /** Width in pixels. */
  readonly width: number;
  /** Height in pixels. */
  readonly height: number;
}

/** A rectangle on a canvas, in pixels. */
export interface PlacedRect extends ImageSize {
  /** Distance of the left edge from the left of the canvas. */
  readonly x: number;
  /** Distance of the top edge from the top of the canvas. */
  readonly y: number;
}
