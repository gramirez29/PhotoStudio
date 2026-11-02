import { describe, expect, it } from 'vitest';
import type { StudioSelectedPhoto } from '../types/api/studio';
import { describeSkippedFinals, matchFinals } from './finalMatching';

const PHOTOS: readonly StudioSelectedPhoto[] = [
  { id: 'a', name: 'IMG_0001.CR2', isExtra: false, finalBytes: null, finalWidth: null, finalHeight: null, finalCapturedAt: null, finalDownloads: 0 },
  { id: 'b', name: 'IMG_0002.jpg', isExtra: false, finalBytes: null, finalWidth: null, finalHeight: null, finalCapturedAt: null, finalDownloads: 0 },
  { id: 'c', name: 'IMG_00020.jpg', isExtra: true, finalBytes: null, finalWidth: null, finalHeight: null, finalCapturedAt: null, finalDownloads: 0 },
];

/**
 * Builds a file of a given name and size.
 * @param name Name of the file.
 * @param size Size in bytes.
 * @param type Content type.
 * @returns The file.
 */
function file(name: string, size = 100, type = 'image/jpeg'): File {
  return new File([new Uint8Array(size)], name, { type });
}

/**
 * Builds a file that says it weighs more than 50 MB, without really holding that much.
 * @param name Name of the file.
 * @returns The file.
 */
function heavy(name: string): File {
  const big = file(name);
  Object.defineProperty(big, 'size', { value: 52_428_801 });
  return big;
}

describe('matchFinals', () => {
  it('pairs a file with the photo of the same name, whatever the extension and the case', () => {
    const { matches, skipped } = matchFinals([file('img_0001.JPG'), file('IMG_0002.jpeg')], PHOTOS);

    expect(matches.map((match) => [match.file.name, match.photoId])).toEqual([
      ['img_0001.JPG', 'a'],
      ['IMG_0002.jpeg', 'b'],
    ]);
    expect(skipped).toEqual([]);
  });

  it('pairs what an editing program adds after the name, and never takes the file of a longer name', () => {
    const { matches } = matchFinals([file('IMG_0001-Edit.jpg'), file('IMG_0002_2.jpg'), file('IMG_00020.jpg')], PHOTOS);

    expect(matches.map((match) => match.photoId)).toEqual(['a', 'b', 'c']);
  });

  it('prefers the exact name over one that only starts the same', () => {
    expect(matchFinals([file('IMG_00020.jpg')], PHOTOS).matches[0]?.photoId).toBe('c');
  });

  it('leaves out files that match no photo, repeat a photo, are not JPEG, are empty or weigh more than 50 MB', () => {
    const { matches, skipped } = matchFinals(
      [file('IMG_0001.jpg'), file('IMG_0001-copy.jpg'), file('other.jpg'), file('IMG_0002.png', 10, 'image/png'), file('IMG_0002.jpg', 0), heavy('IMG_0002.jpg')],
      PHOTOS,
    );

    expect(matches.map((match) => match.photoId)).toEqual(['a']);
    expect(skipped.map((skip) => skip.reason)).toEqual(['duplicate', 'no_match', 'not_jpeg', 'empty', 'too_big']);
  });

  it('says in words what was left out, and nothing when every file was taken', () => {
    expect(describeSkippedFinals(matchFinals([file('IMG_0001.jpg')], PHOTOS))).toBeNull();
    const message = describeSkippedFinals(matchFinals([file('x.jpg'), file('y.jpg'), file('IMG_0001.jpg'), file('IMG_0001-b.jpg')], PHOTOS));

    expect(message).toBe('No se tomaron: 2 no coinciden con ninguna foto que eligió el cliente, 1 repite una foto que ya tiene archivo.');
  });
});
