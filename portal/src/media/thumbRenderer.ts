import { THUMB_LONG_SIDE, THUMB_MAX_BYTES, THUMB_MAX_SHRINKS, THUMB_QUALITIES, THUMB_SHRINK_FACTOR } from '../constants/finals';
import type { ThumbRenderer, ThumbRendererDeps } from '../types/media/thumbRenderer.types';
import { fitWithin } from '../utils/proofImage';

/**
 * Creates the renderer of thumbnails. A thumbnail is only to look at in a grid: the real photo opens when it is tapped. It is
 * reduced to a good-quality small JPEG, tries a lower quality when it weighs too much and, in the end, a smaller size; it carries no
 * watermark, because it comes from a final that the client already paid. The metadata of the photo is not copied: drawing on a
 * canvas leaves only the pixels.
 * @param deps What the renderer needs from the browser.
 * @returns The renderer.
 */
export function createThumbRenderer(deps: ThumbRendererDeps): ThumbRenderer {
  return {
    async render(file) {
      let image: ImageBitmap;
      try {
        image = await deps.decode(file as File);
      } catch {
        return null;
      }

      try {
        let longSide = THUMB_LONG_SIDE;
        for (let shrink = 0; shrink <= THUMB_MAX_SHRINKS; shrink++) {
          const size = fitWithin(image, longSide);
          const canvas = deps.createCanvas(size);
          const context = canvas.getContext();
          if (context === null) {
            return null;
          }

          context.drawImage(image, 0, 0, size.width, size.height);
          for (const quality of THUMB_QUALITIES) {
            const blob = await canvas.toJpeg(quality);
            if (blob !== null && blob.size <= THUMB_MAX_BYTES) {
              return blob;
            }
          }

          longSide = Math.round(longSide * THUMB_SHRINK_FACTOR);
        }

        return null;
      } finally {
        image.close();
      }
    },
  };
}
