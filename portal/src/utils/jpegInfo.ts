import { CAMERA_UTC_OFFSET_MINUTES } from '../constants/finals';
import type { JpegInfo } from '../types/utils/jpegInfo.types';

/** A JPEG whose start tells nothing. */
const UNKNOWN: JpegInfo = { width: null, height: null, capturedAt: null };

/** Tag of the pointer to the Exif directory, in the first directory of a TIFF header. */
const TAG_EXIF_POINTER = 0x8769;
/** Tag of the orientation of the camera. */
const TAG_ORIENTATION = 0x0112;
/** Tag of the date the file was changed, in the first directory; used when the date taken is missing. */
const TAG_DATE_TIME = 0x0132;
/** Tag of the date the photo was taken, in the Exif directory. */
const TAG_DATE_TIME_ORIGINAL = 0x9003;
/** Tag of the offset from UTC of the date taken, in the Exif directory. */
const TAG_OFFSET_TIME_ORIGINAL = 0x9011;

/**
 * Tells whether a marker of a JPEG starts a frame, which holds the size of the image. The markers 0xC4, 0xC8 and 0xCC are not frames.
 * @param marker Second byte of the marker.
 * @returns True for a start of frame.
 */
function isStartOfFrame(marker: number): boolean {
  return marker >= 0xc0 && marker <= 0xcf && marker !== 0xc4 && marker !== 0xc8 && marker !== 0xcc;
}

/**
 * Reads the text of an ASCII entry of a TIFF directory, which is stored apart from the entry when it is longer than four bytes.
 * @param view The bytes.
 * @param tiff Where the TIFF header starts.
 * @param entry Where the entry starts.
 * @param little True when the numbers are little endian.
 * @returns The text without its terminator, or null when it does not fit in the bytes.
 */
function readAscii(view: DataView, tiff: number, entry: number, little: boolean): string | null {
  const count = view.getUint32(entry + 4, little);
  const start = count > 4 ? tiff + view.getUint32(entry + 8, little) : entry + 8;
  if (count < 1 || count > 64 || start + count > view.byteLength) {
    return null;
  }

  let text = '';
  for (let index = 0; index < count; index++) {
    const code = view.getUint8(start + index);
    if (code === 0) {
      break;
    }

    text += String.fromCharCode(code);
  }

  return text;
}

/**
 * Turns the date a camera writes (`2026:10:17 15:30:00`) into an instant. The camera writes no time zone: the one the file gives is
 * used when there is one, and otherwise the one of Costa Rica, which is where the photographer works.
 * @param text The date as the camera wrote it.
 * @param offset The offset from UTC the file gives (`-06:00`), if any.
 * @returns The instant (ISO 8601), or null when the text is not a date.
 */
export function parseCameraDate(text: string, offset: string | null): string | null {
  const match = /^(\d{4}):(\d{2}):(\d{2}) (\d{2}):(\d{2}):(\d{2})$/.exec(text.trim());
  if (match === null) {
    return null;
  }

  const [, year, month, day, hour, minute, second] = match;
  const zone = offset !== null && /^[+-]\d{2}:\d{2}$/.test(offset.trim()) ? offset.trim() : minutesToOffset(CAMERA_UTC_OFFSET_MINUTES);
  const instant = new Date(`${year}-${month}-${day}T${hour}:${minute}:${second}${zone}`);
  return Number.isNaN(instant.getTime()) ? null : instant.toISOString();
}

/**
 * Writes an offset from UTC in minutes as `+HH:MM`.
 * @param minutes Offset in minutes, negative west of Greenwich.
 * @returns The text.
 */
function minutesToOffset(minutes: number): string {
  const sign = minutes < 0 ? '-' : '+';
  const absolute = Math.abs(minutes);
  return `${sign}${String(Math.floor(absolute / 60)).padStart(2, '0')}:${String(absolute % 60).padStart(2, '0')}`;
}

/**
 * Reads the Exif block of a JPEG: the orientation and the date the photo was taken. Anything outside the bytes stops the reading
 * and keeps what was found, because the metadata of a real file is often damaged.
 * @param view The bytes of the file.
 * @param start Where the Exif data starts, after its `Exif` header.
 * @param end Where the segment ends.
 * @returns The orientation (1 when unknown) and the date taken, if any.
 */
function readExif(view: DataView, start: number, end: number): { readonly orientation: number; readonly capturedAt: string | null } {
  let orientation = 1;
  let taken: string | null = null;
  let changed: string | null = null;
  let offset: string | null = null;

  try {
    const little = view.getUint16(start, false) === 0x4949;
    if (view.getUint16(start + 2, little) !== 42) {
      return { orientation, capturedAt: null };
    }

    /**
     * Visits the entries of a directory.
     * @param directory Where the directory starts, from the TIFF header.
     * @param visit Called with the tag and the place of each entry.
     */
    const walk = (directory: number, visit: (tag: number, entry: number) => void): void => {
      const first = start + directory;
      const count = view.getUint16(first, little);
      for (let index = 0; index < count && first + 2 + (index + 1) * 12 <= end; index++) {
        const entry = first + 2 + index * 12;
        visit(view.getUint16(entry, little), entry);
      }
    };

    let exifDirectory = 0;
    walk(view.getUint32(start + 4, little), (tag, entry) => {
      if (tag === TAG_ORIENTATION) {
        orientation = view.getUint16(entry + 8, little);
      } else if (tag === TAG_EXIF_POINTER) {
        exifDirectory = view.getUint32(entry + 8, little);
      } else if (tag === TAG_DATE_TIME) {
        changed = readAscii(view, start, entry, little);
      }
    });

    if (exifDirectory > 0) {
      walk(exifDirectory, (tag, entry) => {
        if (tag === TAG_DATE_TIME_ORIGINAL) {
          taken = readAscii(view, start, entry, little);
        } else if (tag === TAG_OFFSET_TIME_ORIGINAL) {
          offset = readAscii(view, start, entry, little);
        }
      });
    }
  } catch {
    // Damaged metadata: what was read before the damage is kept.
  }

  const text: string | null = taken ?? changed;
  return { orientation, capturedAt: text === null ? null : parseCameraDate(text, taken === null ? null : offset) };
}

/**
 * Reads the size in pixels and the date a photo was taken from the first bytes of a JPEG file. The size comes from the frame header
 * and the date from the Exif block; when the camera rotated the photo, the sides are swapped so the size is the one the photo is
 * shown with. It never throws: a file that is not a JPEG, or is cut, gives what could be read.
 * @param bytes The first bytes of the file (the metadata is at the start, so a megabyte is plenty).
 * @returns What was found.
 */
export function parseJpegInfo(bytes: Uint8Array): JpegInfo {
  if (bytes.length < 4 || bytes[0] !== 0xff || bytes[1] !== 0xd8) {
    return UNKNOWN;
  }

  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  let width: number | null = null;
  let height: number | null = null;
  let capturedAt: string | null = null;
  let orientation = 1;
  let offset = 2;

  while (offset + 4 <= bytes.length) {
    if (bytes[offset] !== 0xff) {
      offset++;
      continue;
    }

    const marker = bytes[offset + 1] ?? 0;
    if (marker === 0xff || marker === 0x00 || marker === 0x01 || (marker >= 0xd0 && marker <= 0xd8)) {
      offset += marker === 0xff ? 1 : 2;
      continue;
    }

    if (marker === 0xd9 || marker === 0xda) {
      break;
    }

    const length = view.getUint16(offset + 2, false);
    if (length < 2) {
      break;
    }

    const start = offset + 4;
    const end = Math.min(offset + 2 + length, bytes.length);
    const isExif = marker === 0xe1 && start + 6 <= end && bytes[start] === 0x45 && bytes[start + 1] === 0x78 && bytes[start + 2] === 0x69 && bytes[start + 3] === 0x66;
    if (isExif && capturedAt === null) {
      const exif = readExif(view, start + 6, end);
      capturedAt = exif.capturedAt;
      orientation = exif.orientation;
    } else if (isStartOfFrame(marker) && start + 5 <= end) {
      height = view.getUint16(start + 1, false);
      width = view.getUint16(start + 3, false);
    }

    offset += 2 + length;
  }

  const turned = orientation >= 5 && orientation <= 8;
  const valid = width !== null && height !== null && width > 0 && height > 0;
  return { width: valid ? (turned ? height : width) : null, height: valid ? (turned ? width : height) : null, capturedAt };
}
