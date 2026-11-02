import type { ProofCanvas } from '../types/media/proofRenderer.types';
import type { ImageSize } from '../types/utils/proofImage.types';

/**
 * Decodes a photo in the browser, applying the rotation the camera recorded, so what is drawn is what the photographer saw.
 * @param file The photo.
 * @returns The decoded image; it must be closed when it is no longer needed.
 */
export function decodeInBrowser(file: File): Promise<ImageBitmap> {
  return createImageBitmap(file, { imageOrientation: 'from-image' });
}

/**
 * Creates a canvas of the browser to draw on and read back as a JPEG.
 * @param size Size of the canvas in pixels.
 * @returns The canvas.
 */
export function createBrowserCanvas(size: ImageSize): ProofCanvas {
  const canvas = document.createElement('canvas');
  canvas.width = size.width;
  canvas.height = size.height;
  return {
    getContext: () => canvas.getContext('2d'),
    toJpeg: (quality) => new Promise((resolve) => canvas.toBlob(resolve, 'image/jpeg', quality)),
  };
}
