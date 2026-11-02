import { matchFinals } from '../finalMatching';
import { readCapturedAt } from '../exifDate';
import { fitWithin, placeWatermark, proofLabel } from '../proofImage';
import { isJpegFile, selectProofFiles } from '../uploadFiles';
import type { SelectedPhoto } from '../../types/api/gallery';
import type { PickedFile } from '../../types/utils/upload.types';

/**
 * Builds a picked file.
 * @param name File name.
 * @param bytes Size.
 * @returns The file.
 */
function file(name: string, bytes = 100): PickedFile {
  return { uri: `file:///cache/${name}`, name, bytes, mimeType: 'image/jpeg', width: 100, height: 50, capturedAt: null };
}

/**
 * Builds a photo of the selection.
 * @param id Identifier.
 * @param name Original name.
 * @returns The photo.
 */
function photo(id: string, name: string): SelectedPhoto {
  return { id, name, isExtra: false, finalBytes: null, finalWidth: null, finalHeight: null, finalCapturedAt: null, finalDownloads: 0, thumbUrl: null, thumbIsFinal: false };
}

describe('image sizes', () => {
  it('shrinks the long side and never enlarges', () => {
    expect(fitWithin({ width: 6000, height: 4000 }, 720)).toEqual({ width: 720, height: 480 });
    expect(fitWithin({ width: 300, height: 200 }, 720)).toEqual({ width: 300, height: 200 });
  });

  it('centers the watermark', () => {
    const place = placeWatermark({ width: 720, height: 480 }, { width: 100, height: 100 }, 0.7);
    expect(place.width).toBeCloseTo(336);
    expect(place.x).toBeCloseTo(192);
  });

  it('never leaves a label empty', () => {
    expect(proofLabel('   ')).toBe('foto.jpg');
  });
});

describe('selecting proofs', () => {
  it('leaves out non-JPEG, duplicates and what does not fit', () => {
    const png = { ...file('a.png'), mimeType: 'image/png' };
    const result = selectProofFiles([png, file('b.jpg'), file('b.jpg'), file('c.jpg'), file('d.jpg')], [], 2);
    expect(result.accepted.map((f) => f.name)).toEqual(['b.jpg', 'c.jpg']);
    expect([result.notJpeg, result.duplicates, result.noRoom]).toEqual([1, 1, 1]);
    expect(isJpegFile({ name: 'x.JPG', mimeType: null })).toBe(true);
  });
});

describe('matching finals', () => {
  const photos = [photo('1', 'IMG_0001.CR2'), photo('2', 'IMG_00010.CR2')];

  it('pairs by name ignoring extension and editing suffixes', () => {
    const result = matchFinals([file('IMG_0001-Edit.jpg'), file('IMG_00010.jpg'), file('other.jpg'), file('IMG_0001_2.jpg')], photos);
    expect(result.matches.map((m) => m.photoId)).toEqual(['1', '2']);
    expect(result.skipped.map((s) => s.reason)).toEqual(['no_match', 'duplicate']);
  });
});

describe('capture date', () => {
  it('reads the camera date with the Costa Rica offset by default', () => {
    expect(readCapturedAt({ DateTimeOriginal: '2026:10:17 14:22:10' }, Date.parse('2026-11-01'))).toBe('2026-10-17T20:22:10.000Z');
    expect(readCapturedAt({ DateTimeOriginal: '1980:01:01 00:00:00' }, Date.now())).toBeNull();
    expect(readCapturedAt(null, Date.now())).toBeNull();
  });
});
