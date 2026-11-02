import watermarkUrl from '../assets/proof-watermark.png';
import type { ProofRenderer, ProofRendererDeps } from '../types/media/proofRenderer.types';
import { createBrowserCanvas, decodeInBrowser } from './browserCanvas';
import { createProofRenderer } from './proofRenderer';

/**
 * What the renderer needs, taken from the browser: `createImageBitmap` turns the photo the way the camera held it, a canvas draws
 * and encodes it, and the watermark is the bundled logo of the app.
 * @param watermark Address of the watermark image.
 * @returns The dependencies of the renderer.
 */
export function createBrowserProofDeps(watermark: string): ProofRendererDeps {
  return {
    decode: decodeInBrowser,
    createCanvas: createBrowserCanvas,
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
