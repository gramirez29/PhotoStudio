import { THUMB_LONG_SIDE, THUMB_MAX_BYTES, THUMB_MAX_SHRINKS, THUMB_QUALITIES, THUMB_SHRINK_FACTOR } from '../constants/upload';
import type { ThumbEngine, ThumbRenderer } from '../types/media/upload.types';

/**
 * Creates the renderer of thumbnails. A thumbnail is only to look at in a grid: the real photo opens when it is tapped. It is
 * reduced to a good-quality small JPEG, tries a lower quality when it weighs too much and, in the end, a smaller size; it carries no
 * watermark, because it comes from a final that the client already paid. The metadata of the photo is not copied.
 * @param engine What the renderer needs from the device.
 * @returns The renderer.
 */
export function createThumbRenderer(engine: ThumbEngine): ThumbRenderer {
  return {
    async render(file) {
      try {
        let longSide = THUMB_LONG_SIDE;
        for (let shrink = 0; shrink <= THUMB_MAX_SHRINKS; shrink++) {
          for (const quality of THUMB_QUALITIES) {
            const thumb = await engine.make(file.uri, longSide, quality);
            if (thumb === null) {
              return null;
            }

            if (thumb.bytes <= THUMB_MAX_BYTES) {
              return thumb;
            }

            engine.discard(thumb.uri);
          }

          longSide = Math.round(longSide * THUMB_SHRINK_FACTOR);
        }

        return null;
      } catch {
        return null;
      }
    },
  };
}
