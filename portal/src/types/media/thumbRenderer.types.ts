import type { ProofRendererDeps } from './proofRenderer.types';

/** What the renderer of thumbnails needs from the browser: decoding a photo and drawing it on a canvas. */
export type ThumbRendererDeps = Pick<ProofRendererDeps, 'decode' | 'createCanvas'>;

/** Makes the small image of a final that the galleries show. */
export interface ThumbRenderer {
  /**
   * Turns a final into a thumbnail: the same photo, reduced to a good-quality small image, with no watermark and nothing else added.
   * @param file The final.
   * @returns The thumbnail, or null when it could not be made to fit; a final does not depend on its thumbnail.
   */
  render(file: Blob): Promise<Blob | null>;
}
