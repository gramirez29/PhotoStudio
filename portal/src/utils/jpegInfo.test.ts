import { describe, expect, it } from 'vitest';
import { parseCameraDate, parseJpegInfo } from './jpegInfo';

/**
 * Builds a JPEG that has only what the reader looks at: the start marker, an Exif block with the date and the orientation, and the
 * header of a frame with the size.
 * @param options What the file says.
 * @returns The bytes.
 */
function jpeg(options: { readonly little?: boolean; readonly width?: number; readonly height?: number; readonly taken?: string; readonly orientation?: number; readonly offset?: string }): Uint8Array {
  const little = options.little ?? true;
  const u16 = (value: number): number[] => (little ? [value & 0xff, value >> 8] : [value >> 8, value & 0xff]);
  const u32 = (value: number): number[] => (little ? [value & 0xff, (value >> 8) & 0xff, (value >> 16) & 0xff, value >> 24] : [value >> 24, (value >> 16) & 0xff, (value >> 8) & 0xff, value & 0xff]);
  const ascii = (text: string): number[] => [...[...text].map((char) => char.charCodeAt(0)), 0];

  const taken = options.taken === undefined ? [] : ascii(options.taken);
  const offsetText = options.offset === undefined ? [] : ascii(options.offset);
  // IFD0 with the orientation and the pointer to the Exif directory, then the Exif directory with the date (and its offset), then the text.
  const ifd0Entries = 2;
  const ifd0Size = 2 + ifd0Entries * 12 + 4;
  const exifEntries = (taken.length > 0 ? 1 : 0) + (offsetText.length > 0 ? 1 : 0);
  const exifStart = 8 + ifd0Size;
  const exifSize = 2 + exifEntries * 12 + 4;
  const takenAt = exifStart + exifSize;
  const offsetAt = takenAt + taken.length;
  const tiff = [
    ...(little ? [0x49, 0x49] : [0x4d, 0x4d]), ...u16(42), ...u32(8),
    ...u16(ifd0Entries),
    ...u16(0x0112), ...u16(3), ...u32(1), ...u16(options.orientation ?? 1), 0, 0,
    ...u16(0x8769), ...u16(4), ...u32(1), ...u32(exifStart),
    ...u32(0),
    ...u16(exifEntries),
    ...(taken.length > 0 ? [...u16(0x9003), ...u16(2), ...u32(taken.length), ...u32(takenAt)] : []),
    ...(offsetText.length > 0 ? [...u16(0x9011), ...u16(2), ...u32(offsetText.length), ...u32(offsetAt)] : []),
    ...u32(0),
    ...taken,
    ...offsetText,
  ];
  const exif = [0x45, 0x78, 0x69, 0x66, 0, 0, ...tiff];
  const app1 = [0xff, 0xe1, (exif.length + 2) >> 8, (exif.length + 2) & 0xff, ...exif];
  const width = options.width ?? 6000;
  const height = options.height ?? 4000;
  const frame = [0xff, 0xc0, 0, 11, 8, height >> 8, height & 0xff, width >> 8, width & 0xff, 1, 1, 0x11, 0];
  return new Uint8Array([0xff, 0xd8, ...app1, ...frame, 0xff, 0xda, 0, 2]);
}

describe('parseJpegInfo', () => {
  it('reads the size and the date taken of a photo, in the time zone of Costa Rica when the file gives none', () => {
    expect(parseJpegInfo(jpeg({ width: 6000, height: 4000, taken: '2026:10:17 15:30:00' }))).toEqual({
      width: 6000,
      height: 4000,
      capturedAt: '2026-10-17T21:30:00.000Z',
    });
  });

  it('reads files written by cameras of either byte order', () => {
    expect(parseJpegInfo(jpeg({ little: false, width: 1250, height: 3000, taken: '2026:01:02 03:04:05' }))).toMatchObject({ width: 1250, height: 3000, capturedAt: '2026-01-02T09:04:05.000Z' });
  });

  it('uses the offset from UTC the file gives instead of assuming one', () => {
    expect(parseJpegInfo(jpeg({ taken: '2026:10:17 15:30:00', offset: '+02:00' })).capturedAt).toBe('2026-10-17T13:30:00.000Z');
  });

  it('swaps the sides when the camera rotated the photo, so the size is the one it is shown with', () => {
    expect(parseJpegInfo(jpeg({ width: 6000, height: 4000, orientation: 6 }))).toMatchObject({ width: 4000, height: 6000 });
    expect(parseJpegInfo(jpeg({ width: 6000, height: 4000, orientation: 3 }))).toMatchObject({ width: 6000, height: 4000 });
  });

  it('gives the size and no date when the file has no metadata', () => {
    expect(parseJpegInfo(jpeg({ taken: undefined }))).toEqual({ width: 6000, height: 4000, capturedAt: null });
  });

  it('gives nothing for what is not a JPEG, is empty or is cut in the middle of the metadata, and never throws', () => {
    const empty = { width: null, height: null, capturedAt: null };
    expect(parseJpegInfo(new Uint8Array([]))).toEqual(empty);
    expect(parseJpegInfo(new Uint8Array([0x89, 0x50, 0x4e, 0x47, 1, 2, 3, 4]))).toEqual(empty);
    const whole = jpeg({ taken: '2026:10:17 15:30:00' });
    expect(() => parseJpegInfo(whole.slice(0, 30))).not.toThrow();
    expect(parseJpegInfo(whole.slice(0, 30)).width).toBeNull();
  });
});

describe('parseCameraDate', () => {
  it('turns what a camera writes into an instant and refuses anything else', () => {
    expect(parseCameraDate('2026:10:17 15:30:00', null)).toBe('2026-10-17T21:30:00.000Z');
    expect(parseCameraDate('2026:13:45 99:99:99', null)).toBeNull();
    expect(parseCameraDate('yesterday', null)).toBeNull();
    expect(parseCameraDate('2026:10:17 15:30:00', 'bad')).toBe('2026-10-17T21:30:00.000Z');
  });
});
