import {
  PROOF_LONG_SIDE,
  PROOF_MAX_BYTES,
  PROOF_MAX_SHRINKS,
  PROOF_QUALITIES,
  PROOF_SHRINK_FACTOR,
  WATERMARK_OPACITY,
  WATERMARK_SHADOW_RATIO,
  WATERMARK_SIZE_RATIO,
} from '../constants/proof';
import type { ProofCanvas, ProofRenderFailure, ProofRenderer, ProofRendererDeps, RenderedProof } from '../types/media/proofRenderer.types';
import type { ImageSize } from '../types/utils/proofImage.types';
import { fitWithin, placeWatermark } from '../utils/proofImage';

/** A photo could not be turned into a proof; `kind` says why, so the page can say it in words. */
export class ProofRenderError extends Error {
  /** Why the photo failed. */
  public readonly kind: ProofRenderFailure;

  /**
   * Creates the error.
   * @param kind Why the photo failed.
   */
  public constructor(kind: ProofRenderFailure) {
    super(`The photo could not be turned into a proof (${kind}).`);
    this.name = 'ProofRenderError';
    this.kind = kind;
  }
}

/**
 * Draws the watermark in the middle of the proof: big, a little see-through and with a soft shadow, so it is seen on a dark photo and
 * on a white one, and cropping the photo cannot take it away.
 * @param context Drawing context of the proof.
 * @param mark The watermark image.
 * @param size Size of the proof.
 */
function drawWatermark(context: CanvasRenderingContext2D, mark: ImageBitmap, size: ImageSize): void {
  const place = placeWatermark(size, mark, WATERMARK_SIZE_RATIO);
  context.save();
  context.globalAlpha = WATERMARK_OPACITY;
  context.shadowColor = 'rgba(0, 0, 0, 0.7)';
  context.shadowBlur = Math.min(size.width, size.height) * WATERMARK_SHADOW_RATIO;
  context.drawImage(mark, place.x, place.y, place.width, place.height);
  context.restore();
}

/**
 * Creates the renderer of proofs. A proof is the photo reduced to a small size, encoded as a JPEG of low quality and with the logo
 * of the app across the middle. It is made in the browser: the full photo never leaves the computer of the photographer, and the
 * server never has to do the work. If the proof still weighs more than the limit with the lowest quality, it is made smaller.
 * @param deps What the renderer needs from the browser.
 * @returns The renderer.
 */
export function createProofRenderer(deps: ProofRendererDeps): ProofRenderer {
  let watermark: Promise<ImageBitmap> | null = null;

  /**
   * Loads the watermark the first time and gives the same one afterwards.
   * @returns The watermark image.
   */
  function getWatermark(): Promise<ImageBitmap> {
    watermark ??= deps.loadWatermark();
    return watermark;
  }

  /**
   * Draws the photo and the watermark on a canvas of a given size.
   * @param image The decoded photo.
   * @param mark The watermark image.
   * @param size Size of the proof.
   * @returns The canvas.
   */
  function draw(image: ImageBitmap, mark: ImageBitmap, size: ImageSize): ProofCanvas {
    const canvas = deps.createCanvas(size);
    const context = canvas.getContext();
    if (context === null) {
      throw new ProofRenderError('canvas');
    }

    context.drawImage(image, 0, 0, size.width, size.height);
    drawWatermark(context, mark, size);
    return canvas;
  }

  return {
    async render(file: File): Promise<RenderedProof> {
      let image: ImageBitmap;
      let mark: ImageBitmap;
      try {
        [image, mark] = await Promise.all([deps.decode(file), getWatermark()]);
      } catch {
        throw new ProofRenderError('decode');
      }

      try {
        let longSide = PROOF_LONG_SIDE;
        for (let shrink = 0; shrink <= PROOF_MAX_SHRINKS; shrink++) {
          const size = fitWithin(image, longSide);
          const canvas = draw(image, mark, size);
          for (const quality of PROOF_QUALITIES) {
            const blob = await canvas.toJpeg(quality);
            if (blob === null) {
              throw new ProofRenderError('canvas');
            }

            if (blob.size <= PROOF_MAX_BYTES) {
              return { blob, size, quality };
            }
          }

          longSide = Math.round(longSide * PROOF_SHRINK_FACTOR);
        }

        throw new ProofRenderError('too_big');
      } finally {
        image.close();
      }
    },
  };
}
