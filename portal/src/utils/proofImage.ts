import { PROOF_NAME_MAX_LENGTH } from '../constants/proof';
import type { ImageSize, PlacedRect } from '../types/utils/proofImage.types';

/**
 * Tells whether a file is a JPEG, by its type and, when the browser gave none, by its extension. The API checks the bytes that arrive
 * in the storage anyway; this only keeps the page from preparing a file that can never be a proof.
 * @param file The file the person chose.
 * @returns True for a JPEG.
 */
export function isJpegFile(file: Pick<File, 'name' | 'type'>): boolean {
  if (file.type !== '') {
    return file.type === 'image/jpeg';
  }

  return /\.jpe?g$/i.test(file.name);
}

/**
 * Cleans the name of a file to be kept as a label of the proof: no control characters, no more than the API accepts, never empty.
 * @param name Name of the file.
 * @returns The name to register.
 */
export function proofLabel(name: string): string {
  // eslint-disable-next-line no-control-regex -- control characters are exactly what is removed
  const clean = name.replace(/[\u0000-\u001f\u007f]/g, '').trim();
  return clean === '' ? 'foto.jpg' : clean.slice(0, PROOF_NAME_MAX_LENGTH);
}

/**
 * Scales a size down so its longer side is at most a given length, keeping the proportions. A smaller image is never enlarged.
 * @param size Size of the original.
 * @param longSide Longest side allowed.
 * @returns The size of the proof, at least one pixel on each side.
 */
export function fitWithin(size: ImageSize, longSide: number): ImageSize {
  const longest = Math.max(size.width, size.height);
  if (longest <= longSide) {
    return { width: Math.max(1, Math.round(size.width)), height: Math.max(1, Math.round(size.height)) };
  }

  const scale = longSide / longest;
  return { width: Math.max(1, Math.round(size.width * scale)), height: Math.max(1, Math.round(size.height * scale)) };
}

/**
 * Places the watermark in the middle of the proof, as wide as a fraction of the shorter side, without changing its proportions. It
 * never gets wider than the proof, nor taller.
 * @param canvas Size of the proof.
 * @param mark Size of the watermark image.
 * @param ratio Width of the watermark as a fraction of the shorter side of the proof.
 * @returns Where to draw it.
 */
export function placeWatermark(canvas: ImageSize, mark: ImageSize, ratio: number): PlacedRect {
  const target = Math.min(canvas.width, canvas.height) * ratio;
  const scale = Math.min(target / mark.width, canvas.width / mark.width, canvas.height / mark.height);
  const width = mark.width * scale;
  const height = mark.height * scale;
  return { x: (canvas.width - width) / 2, y: (canvas.height - height) / 2, width, height };
}
