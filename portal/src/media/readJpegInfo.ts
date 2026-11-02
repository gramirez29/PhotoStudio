import { JPEG_INFO_READ_BYTES } from '../constants/finals';
import type { JpegInfo } from '../types/utils/jpegInfo.types';
import { parseJpegInfo } from '../utils/jpegInfo';

/**
 * Reads the size in pixels and the date taken of a JPEG file chosen by the photographer, from its first megabyte: the photo is not
 * decoded and the whole file is not loaded into memory. It never fails: what cannot be read comes back unknown, because the data is
 * information for the photographer and never a reason to refuse a photo.
 * @param file The file.
 * @returns What was found.
 */
export async function readJpegInfo(file: Blob): Promise<JpegInfo> {
  try {
    return parseJpegInfo(new Uint8Array(await file.slice(0, JPEG_INFO_READ_BYTES).arrayBuffer()));
  } catch {
    return { width: null, height: null, capturedAt: null };
  }
}
