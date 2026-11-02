import { CAMERA_UTC_OFFSET_MINUTES } from '../constants/upload';
import { isRecord } from '../api/guards';

/** What a camera writes for a date: `2026:10:17 14:22:10`, with colons between the parts of the day. */
const EXIF_DATE = /^(\d{4}):(\d{2}):(\d{2}) (\d{2}):(\d{2}):(\d{2})/;

/** What a camera writes for the offset of the date: `-06:00`. */
const EXIF_OFFSET = /^([+-])(\d{2}):(\d{2})$/;

/** Earliest year a photo can have been taken: a date before it is a camera that was never set. */
const EARLIEST_YEAR = 1990;

/**
 * Reads the instant a photo was taken from the EXIF the picker returned. A camera writes the local date without a time zone, so
 * the offset the camera wrote is used when there is one, and Costa Rica's otherwise. What cannot be read, or makes no sense (before
 * 1990 or in the future), is unknown rather than wrong.
 * @param exif What the picker returned as EXIF, if anything.
 * @param now Current instant in milliseconds since the epoch, to tell a date in the future.
 * @returns The instant (ISO 8601), or null.
 */
export function readCapturedAt(exif: unknown, now: number): string | null {
  if (!isRecord(exif)) {
    return null;
  }

  const raw = exif.DateTimeOriginal ?? exif.DateTimeDigitized ?? exif.DateTime;
  const match = typeof raw === 'string' ? EXIF_DATE.exec(raw.trim()) : null;
  if (match === null) {
    return null;
  }

  const [, year, month, day, hour, minute, second] = match.map(Number);
  const offsetText = typeof exif.OffsetTimeOriginal === 'string' ? exif.OffsetTimeOriginal : typeof exif.OffsetTime === 'string' ? exif.OffsetTime : null;
  const offsetMatch = offsetText === null ? null : EXIF_OFFSET.exec(offsetText.trim());
  const offsetMinutes = offsetMatch === null ? CAMERA_UTC_OFFSET_MINUTES : (offsetMatch[1] === '-' ? -1 : 1) * (Number(offsetMatch[2]) * 60 + Number(offsetMatch[3]));

  const utc = Date.UTC(year ?? 0, (month ?? 1) - 1, day ?? 1, hour ?? 0, minute ?? 0, second ?? 0) - offsetMinutes * 60_000;
  if (Number.isNaN(utc) || new Date(utc).getUTCFullYear() < EARLIEST_YEAR || utc > now) {
    return null;
  }

  return new Date(utc).toISOString();
}
