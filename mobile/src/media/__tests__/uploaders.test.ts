import { createFinalUploader } from '../finalUploader';
import { createProofRenderer } from '../proofRenderer';
import { createProofUploader } from '../proofUploader';
import { createZipUploader } from '../zipUploader';
import { parseGallery } from '../../api/galleryApi';
import { galleryPayload } from '../../api/__fixtures__/galleryPayloads';
import type { UploadItem, UploadObserver } from '../../types/media/upload.types';

const gallery = parseGallery(galleryPayload);
const slot = { photoId: 'p1', url: 'https://s/x', headers: { 'Content-Type': 'image/jpeg' }, expiresAt: 'x' };
const file = { uri: 'file:///c/a.jpg', name: 'a.jpg', bytes: 10, mimeType: 'image/jpeg', width: 10, height: 10, capturedAt: null };
const item: UploadItem = { id: 1, file, status: 'waiting', problem: null, progress: 0 };

/**
 * Builds an observer that records what it is told.
 * @returns The observer and its log.
 */
function observer(): { readonly o: UploadObserver; readonly log: string[] } {
  const log: string[] = [];
  return {
    log,
    o: { onItem: (id, s) => log.push(`${id}:${s}`), onProgress: () => undefined, onDone: (id) => log.push(`${id}:done`), onRegistered: () => log.push('registered') },
  };
}

describe('proof renderer', () => {
  it('lowers the quality until the proof fits', async () => {
    const sizes = [2_000_000, 500_000];
    const encode = jest.fn(async () => new Uint8Array(sizes.shift() ?? 1));
    const renderer = createProofRenderer({ open: async () => ({ width: 6000, height: 4000, encode, close: jest.fn() }) });
    const proof = await renderer.render('x');
    expect(proof.quality).toBe(30);
    expect(proof.size).toEqual({ width: 720, height: 480 });
  });

  it('says why it failed', async () => {
    const renderer = createProofRenderer({ open: () => Promise.reject(new Error('x')) });
    await expect(renderer.render('x')).rejects.toMatchObject({ kind: 'decode' });
  });
});

describe('proof uploader', () => {
  it('prepares, uploads, registers and cleans up', async () => {
    const discard = jest.fn();
    const uploader = createProofUploader({
      api: { requestProofUploads: jest.fn(async () => [slot]), registerProofs: jest.fn(async () => ({ gallery, rejected: [] })) },
      renderer: { render: async () => ({ bytes: new Uint8Array(5), size: { width: 1, height: 1 }, quality: 40 }) },
      staging: { stage: () => ({ uri: 'file:///staged', bytes: 5 }), discard },
      storage: { put: jest.fn(async () => undefined) },
    });
    const { o, log } = observer();
    const outcome = await uploader.run('g', [item], o);
    expect(outcome).toEqual({ stopped: false, problem: null });
    expect(log).toEqual(['1:preparing', '1:uploading', '1:registering', '1:done', 'registered']);
    expect(discard).toHaveBeenCalledWith('file:///staged');
  });

  it('stops and marks the batch when the API fails', async () => {
    const uploader = createProofUploader({
      api: { requestProofUploads: () => Promise.reject(new Error('offline')), registerProofs: jest.fn() },
      renderer: { render: async () => ({ bytes: new Uint8Array(5), size: { width: 1, height: 1 }, quality: 40 }) },
      staging: { stage: () => ({ uri: 's', bytes: 5 }), discard: jest.fn() },
      storage: { put: jest.fn() },
    });
    const { o, log } = observer();
    const outcome = await uploader.run('g', [item], o);
    expect(outcome.stopped).toBe(true);
    expect(log).toContain('1:failed');
  });
});

describe('final and zip uploaders', () => {
  it('registers finals with what the phone knows', async () => {
    const registerFinals = jest.fn(async () => ({ gallery, rejected: [] }));
    const uploader = createFinalUploader({
      api: { requestFinalUploads: async () => [{ ...slot, thumbUrl: null, thumbHeaders: {} }], registerFinals },
      thumbs: { render: async () => null },
      staging: { discard: jest.fn() },
      storage: { put: jest.fn(async () => undefined) },
    });
    await uploader.run('g', [{ ...item, photoId: 'p1' }], observer().o);
    expect(registerFinals).toHaveBeenCalledWith('g', ['p1'], [{ photoId: 'p1', width: 10, height: 10, capturedAt: null }], undefined);
  });

  it('uploads the zip and registers it', async () => {
    const uploader = createZipUploader({
      api: { requestZipUpload: async () => ({ url: 'u', headers: {}, expiresAt: 'x' }), registerZip: async () => gallery },
      storage: { put: jest.fn(async () => undefined) },
    });
    expect((await uploader.run('g', file, () => undefined)).gallery).toBe(gallery);
  });
});
