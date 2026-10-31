import watermarkUrl from '../assets/proof-watermark.png';
import type { ProofCanvas, ProofRenderer, ProofRendererDeps } from '../types/media/proofRenderer.types';
import { createProofRenderer } from './proofRenderer';

/**
 * What the renderer needs, taken from the browser: `createImageBitmap` turns the photo the way the camera held it, a canvas draws
 * and encodes it, and the watermark is the bundled logo of the app.
 * @param watermark Address of the watermark image.
 * @returns The dependencies of the renderer.
 */
export function createBrowserProofDeps(watermark: string): ProofRendererDeps {
  return {
    decode: (file) => createImageBitmap(file, { imageOrientation: 'from-image' }),
    createCanvas: (size): ProofCanvas => {
      const canvas = document.createElement('canvas');
      canvas.width = size.width;
      canvas.height = size.height;
      return {
        getContext: () => canvas.getContext('2d'),
        toJpeg: (quality) => new Promise((resolve) => canvas.toBlob(resolve, 'image/jpeg', quality)),
      };
    },
    loadWatermark: async () => {
      const response = await fetch(watermark);
      if (!response.ok) {
        throw new Error(`The watermark could not be loaded (HTTP ${response.status}).`);
      }

      return createImageBitmap(await response.blob());
    },
  };
}

/** The renderer the pages use: it turns photos into proofs in this browser. */
export const browserProofRenderer: ProofRenderer = createProofRenderer(createBrowserProofDeps(watermarkUrl));
