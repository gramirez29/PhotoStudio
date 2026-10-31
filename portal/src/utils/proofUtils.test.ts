import { describe, expect, it } from 'vitest';
import { mapWithLimit } from './concurrency';
import { fitWithin, isJpegFile, placeWatermark, proofLabel } from './proofImage';
import { describeSkipped, selectProofFiles } from './proofSelection';
import { resolveRoute } from './route';

/**
 * Builds a file for the tests.
 * @param name Name of the file.
 * @param type Content type of the file.
 * @param size Size in bytes.
 * @param lastModified Date of the file.
 * @returns The file.
 */
function file(name: string, type = 'image/jpeg', size = 10, lastModified = 1): File {
  return new File([new Uint8Array(size)], name, { type, lastModified });
}

describe('isJpegFile', () => {
  it('accepts the JPEG type and, when the browser gives none, the extension', () => {
    expect(isJpegFile({ name: 'a.jpg', type: 'image/jpeg' })).toBe(true);
    expect(isJpegFile({ name: 'a.JPEG', type: '' })).toBe(true);
    expect(isJpegFile({ name: 'a.jpg', type: 'image/png' })).toBe(false);
    expect(isJpegFile({ name: 'a.png', type: '' })).toBe(false);
  });
});

describe('proofLabel', () => {
  it('removes control characters, trims, caps the length and never gives an empty label', () => {
    expect(proofLabel('  IMG_001\u0000.jpg\n')).toBe('IMG_001.jpg');
    expect(proofLabel('x'.repeat(300))).toHaveLength(200);
    expect(proofLabel('\u0001\u0002')).toBe('foto.jpg');
  });
});

describe('fitWithin', () => {
  it('scales the longer side down and keeps the proportions', () => {
    expect(fitWithin({ width: 6000, height: 4000 }, 720)).toEqual({ width: 720, height: 480 });
    expect(fitWithin({ width: 4000, height: 6000 }, 720)).toEqual({ width: 480, height: 720 });
  });

  it('never enlarges a small image and never gives a side of zero', () => {
    expect(fitWithin({ width: 300, height: 200 }, 720)).toEqual({ width: 300, height: 200 });
    expect(fitWithin({ width: 10000, height: 2 }, 720).height).toBe(1);
  });
});

describe('placeWatermark', () => {
  it('centers the mark and makes it as wide as the ratio of the shorter side', () => {
    const place = placeWatermark({ width: 720, height: 480 }, { width: 640, height: 640 }, 0.7);
    expect(place.width).toBeCloseTo(336);
    expect(place.height).toBeCloseTo(336);
    expect(place.x).toBeCloseTo((720 - 336) / 2);
    expect(place.y).toBeCloseTo((480 - 336) / 2);
  });

  it('never makes the mark bigger than the proof', () => {
    const place = placeWatermark({ width: 100, height: 100 }, { width: 640, height: 640 }, 5);
    expect(place.width).toBeLessThanOrEqual(100);
    expect(place.height).toBeLessThanOrEqual(100);
  });
});

describe('mapWithLimit', () => {
  it('keeps the order of the results and never runs more than the limit at once', async () => {
    let running = 0;
    let peak = 0;
    const results = await mapWithLimit([1, 2, 3, 4, 5, 6], 2, async (item) => {
      running++;
      peak = Math.max(peak, running);
      await new Promise((resolve) => setTimeout(resolve, 10 - item));
      running--;
      return item * 2;
    });
    expect(results).toEqual([2, 4, 6, 8, 10, 12]);
    expect(peak).toBe(2);
  });

  it('gives an empty list for no items', async () => {
    expect(await mapWithLimit([], 3, async (item: number) => item)).toEqual([]);
  });
});

describe('selectProofFiles', () => {
  it('takes JPEG files in order and says what it left out and why', () => {
    const queued = [file('queued.jpg', 'image/jpeg', 5, 9)];
    const selection = selectProofFiles(
      [file('a.jpg'), file('b.png', 'image/png'), file('queued.jpg', 'image/jpeg', 5, 9), file('c.jpg', 'image/jpeg', 10, 2), file('d.jpg', 'image/jpeg', 10, 3)],
      queued,
      2,
    );
    expect(selection.accepted.map((accepted) => accepted.name)).toEqual(['a.jpg', 'c.jpg']);
    expect(selection).toMatchObject({ notJpeg: 1, duplicates: 1, noRoom: 1 });
  });

  it('does not take the same photo twice in one choice', () => {
    const selection = selectProofFiles([file('a.jpg'), file('a.jpg')], [], 10);
    expect(selection.accepted).toHaveLength(1);
    expect(selection.duplicates).toBe(1);
  });

  it('takes nothing when there is no room', () => {
    expect(selectProofFiles([file('a.jpg')], [], 0)).toMatchObject({ accepted: [], noRoom: 1 });
    expect(selectProofFiles([file('a.jpg')], [], -3)).toMatchObject({ accepted: [], noRoom: 1 });
  });
});

describe('describeSkipped', () => {
  it('says nothing when every file was taken', () => {
    expect(describeSkipped({ accepted: [], notJpeg: 0, duplicates: 0, noRoom: 0 })).toBeNull();
  });

  it('counts the files left out and gives each reason', () => {
    expect(describeSkipped({ accepted: [], notJpeg: 1, duplicates: 0, noRoom: 0 })).toBe('Se dejó fuera 1 foto: 1 no es JPEG.');
    expect(describeSkipped({ accepted: [], notJpeg: 2, duplicates: 1, noRoom: 3 })).toBe(
      'Se dejaron fuera 6 fotos: 2 no son JPEG; 1 ya estaba en la lista; 3 no caben en la galería.',
    );
  });
});

describe('the route of a gallery', () => {
  const id = '0197A000-0000-7000-8000-000000000001';

  it('reads the identifier of the gallery from the path, in lower case', () => {
    expect(resolveRoute(`/studio/galleries/${id}`)).toEqual({ kind: 'studioGallery', galleryId: id.toLowerCase() });
    expect(resolveRoute(`/studio/galleries/${id}/`)).toEqual({ kind: 'studioGallery', galleryId: id.toLowerCase() });
  });

  it('does not take anything that is not an identifier', () => {
    expect(resolveRoute('/studio/galleries/abc').kind).toBe('notFound');
    expect(resolveRoute(`/studio/galleries/${id}/extra`).kind).toBe('notFound');
    expect(resolveRoute('/studio/galleries/').kind).toBe('notFound');
  });
});
