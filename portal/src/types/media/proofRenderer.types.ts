import type { ImageSize } from '../utils/proofImage.types';

/** A canvas the renderer draws a proof on, reduced to what it needs. */
export interface ProofCanvas {
  /**
   * Gives the 2D drawing context of the canvas.
   * @returns The context, or null when the browser cannot draw on it.
   */
  getContext(): CanvasRenderingContext2D | null;

  /**
   * Encodes what was drawn as a JPEG. The encoding keeps no metadata of the original (no location, no camera, no orientation tag).
   * @param quality Quality from 0 to 1.
   * @returns The JPEG, or null when the browser could not encode it.
   */
  toJpeg(quality: number): Promise<Blob | null>;
}

/** What the renderer needs from the browser. Tests give another one. */
export interface ProofRendererDeps {
  /**
   * Decodes a photo, already turned the way the camera held it.
   * @param file The photo.
   * @returns The decoded image; the renderer closes it.
   */
  decode(file: File): Promise<ImageBitmap>;

  /**
   * Makes a blank canvas.
   * @param size Size in pixels.
   * @returns The canvas.
   */
  createCanvas(size: ImageSize): ProofCanvas;

  /**
   * Loads the watermark: the logo of the app, white on transparent. It is asked for once per renderer.
   * @returns The watermark image.
   */
  loadWatermark(): Promise<ImageBitmap>;
}

/** A proof ready to be uploaded. */
export interface RenderedProof {
  /** The JPEG, no heavier than a proof may be. */
  readonly blob: Blob;
  /** Size of the proof in pixels. */
  readonly size: ImageSize;
  /** Quality the JPEG was encoded with. */
  readonly quality: number;
}

/** Why a photo could not be turned into a proof. */
export type ProofRenderFailure = 'decode' | 'canvas' | 'too_big';

/** Turns photos into proofs: small, low quality and with the watermark in the middle. */
export interface ProofRenderer {
  /**
   * Turns one photo into a proof.
   * @param file The photo.
   * @returns The proof.
   * @throws ProofRenderError When the photo cannot be read, drawn, or made light enough.
   */
  render(file: File): Promise<RenderedProof>;
}
