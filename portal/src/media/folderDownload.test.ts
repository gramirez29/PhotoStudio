import { describe, expect, it, vi } from 'vitest';
import type { FolderDownloadDeps, FolderDownloadItem } from '../types/media/folderDownload.types';
import { resolveRoute } from '../utils/route';
import { saveFinalsToFolder, uniqueFileName } from './folderDownload';

/**
 * Builds the dependencies of the saving: each photo answers a link named after it and a file of the size given for it.
 * @param sizes Size of the file each photo answers with, by photo identifier.
 * @param names Name each photo comes with, by photo identifier.
 * @returns The dependencies and the files written.
 */
function fakes(sizes: Record<string, number>, names: Record<string, string> = {}): { readonly deps: FolderDownloadDeps; readonly written: string[] } {
  const written: string[] = [];
  return {
    written,
    deps: {
      getLink: async (photoId) => ({ url: `https://storage.test/${photoId}`, fileName: names[photoId] ?? `${photoId}.jpg` }),
      fetchFile: async (url) => {
        const size = sizes[url.slice(url.lastIndexOf('/') + 1)];
        if (size === undefined) {
          throw new Error('missing');
        }

        return new Blob([new Uint8Array(size)]);
      },
      writeFile: async (fileName) => {
        written.push(fileName);
      },
    },
  };
}

const items: readonly FolderDownloadItem[] = [
  { photoId: 'a', name: 'IMG_0001.CR2', bytes: 10 },
  { photoId: 'b', name: 'IMG_0002.CR2', bytes: 20 },
  { photoId: 'c', name: 'IMG_0003.CR2', bytes: 30 },
];

describe('saving the finals into a folder', () => {
  it('writes every final whose size is the recorded one, and tells the progress', async () => {
    const { deps, written } = fakes({ a: 10, b: 20, c: 30 });
    const progress = vi.fn();

    const result = await saveFinalsToFolder(items, deps, progress);

    expect(result).toEqual({ saved: 3, failed: [] });
    expect(written).toEqual(['a.jpg', 'b.jpg', 'c.jpg']);
    expect(progress.mock.calls.map(([handled]) => handled)).toEqual([1, 2, 3]);
  });

  it('does not write a final that arrives with another size, nor one that cannot be received, and goes on with the rest', async () => {
    const { deps, written } = fakes({ a: 10, b: 19 });

    const result = await saveFinalsToFolder(items, deps, () => undefined);

    expect(result.saved).toBe(1);
    expect(result.failed).toEqual([
      { photoId: 'b', name: 'IMG_0002.CR2', reason: 'size' },
      { photoId: 'c', name: 'IMG_0003.CR2', reason: 'failed' },
    ]);
    expect(written).toEqual(['a.jpg']);
  });

  it('keeps two finals that come with the same name from overwriting each other', async () => {
    const { deps, written } = fakes({ a: 10, b: 20 }, { a: 'foto.jpg', b: 'FOTO.jpg' });

    await saveFinalsToFolder(items.slice(0, 2), deps, () => undefined);

    expect(written).toEqual(['foto.jpg', 'FOTO-2.jpg']);
    expect(uniqueFileName('sin-extension', new Set(['sin-extension']))).toBe('sin-extension-2');
  });

  it('stops between two photos when it is cancelled', async () => {
    const { deps, written } = fakes({ a: 10, b: 20, c: 30 });
    const abort = new AbortController();

    const result = await saveFinalsToFolder(items, deps, (handled) => handled === 1 && abort.abort(), abort.signal);

    expect(result.saved).toBe(1);
    expect(written).toEqual(['a.jpg']);
  });
});

describe('the address of the galleries to archive', () => {
  it('is its own page', () => {
    expect(resolveRoute('/studio/archive')).toEqual({ kind: 'studioArchive' });
    expect(resolveRoute('/studio/archive/')).toEqual({ kind: 'studioArchive' });
    expect(resolveRoute('/studio/archivex')).toEqual({ kind: 'notFound' });
  });
});
