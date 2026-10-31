import { describe, expect, it, vi } from 'vitest';
import { ApiError, NetworkError } from '../api/httpClient';
import { ProofUploadError } from '../api/proofStorage';
import { galleryPayload } from '../test/fixtures';
import { parseStudioGallery } from '../api/studioApi';
import type { ProofUploadSlot } from '../types/api/proofs';
import type { ProofRenderer } from '../types/media/proofRenderer.types';
import type { UploadItem, UploadItemStatus, UploadObserver } from '../types/media/proofUploader.types';
import { ProofRenderError } from './proofRenderer';
import { createProofUploader } from './proofUploader';

/**
 * Builds the queue of a run.
 * @param count Number of photos.
 * @returns The items, named `p1.jpg`, `p2.jpg`...
 */
function queue(count: number): UploadItem[] {
  return Array.from({ length: count }, (_, index) => ({
    id: index + 1,
    file: new File([new Uint8Array(4)], `p${index + 1}.jpg`, { type: 'image/jpeg' }),
    status: 'waiting' as const,
    problem: null,
  }));
}

/** Records what the uploader tells the page, so the tests can read the story of every photo. */
interface Recorder {
  /** The observer to give the uploader. */
  readonly observer: UploadObserver;
  /** Status changes by photo, in order. */
  readonly story: Map<number, string[]>;
  /** Photos that were registered. */
  readonly done: number[];
  /** Counts of proofs reported after each batch. */
  readonly registered: number[];
}

/**
 * Makes an observer that writes everything down.
 * @returns The recorder.
 */
function recorder(): Recorder {
  const story = new Map<number, string[]>();
  const done: number[] = [];
  const registered: number[] = [];
  const note = (id: number, text: string): void => void story.set(id, [...(story.get(id) ?? []), text]);
  return {
    story,
    done,
    registered,
    observer: {
      onItem: (id: number, status: UploadItemStatus, problem: string | null) => note(id, problem === null ? status : `${status}: ${problem}`),
      onDone: (id: number) => {
        done.push(id);
        note(id, 'done');
      },
      onRegistered: (gallery) => void registered.push(gallery.photoCount),
    },
  };
}

/** The renderer that turns every photo into a proof of 100 bytes. */
const renderer: ProofRenderer = { render: () => Promise.resolve({ blob: new Blob([new Uint8Array(100)]), size: { width: 10, height: 10 }, quality: 0.4 }) };

/**
 * Builds the answer of the API when the proofs of a batch are registered.
 * @param photoCount Proofs the gallery has after it.
 * @param rejected Proofs not registered.
 * @returns The answer.
 */
function registered(photoCount: number, rejected: { photoId: string; reason: string }[] = []): { gallery: ReturnType<typeof parseStudioGallery>; rejected: { photoId: string; reason: string }[] } {
  return { gallery: parseStudioGallery({ ...galleryPayload, photoCount }, 'gallery'), rejected };
}

/**
 * Builds a fake API whose slots are numbered in the order they are given.
 * @param register What registering answers; defaults to accepting everything.
 * @returns The API and the calls it got.
 */
function fakeApi(register?: (photoIds: string[]) => ReturnType<typeof registered>): {
  api: Parameters<typeof createProofUploader>[0]['api'];
  requests: number[];
  registrations: string[][];
} {
  let slotNumber = 0;
  let total = 0;
  const requests: number[] = [];
  const registrations: string[][] = [];
  return {
    requests,
    registrations,
    api: {
      requestProofUploads: (_id, files) => {
        requests.push(files.length);
        return Promise.resolve(files.map((): ProofUploadSlot => ({ photoId: `photo-${++slotNumber}`, url: `https://storage.test/${slotNumber}?sig=secret`, headers: { 'Content-Type': 'image/jpeg' }, expiresAt: '2026-10-30T12:15:00+00:00' })));
      },
      registerProofs: (_id, photos) => {
        const ids = photos.map((photo) => photo.photoId);
        registrations.push(ids);
        total += ids.length;
        return Promise.resolve(register === undefined ? registered(total) : register(ids));
      },
    },
  };
}

describe('the uploader of proofs', () => {
  it('prepares, sends and registers every photo, ten at a time, and tells the page', async () => {
    const { api, requests, registrations } = fakeApi();
    const put = vi.fn<(slot: ProofUploadSlot, proof: Blob) => Promise<void>>().mockResolvedValue(undefined);
    const record = recorder();

    const outcome = await createProofUploader({ api, renderer, put }).run('g1', queue(23), record.observer);

    expect(outcome).toEqual({ stopped: false, problem: null });
    expect(requests).toEqual([10, 10, 3]);
    expect(registrations.map((batch) => batch.length)).toEqual([10, 10, 3]);
    expect(put).toHaveBeenCalledTimes(23);
    expect(record.done).toHaveLength(23);
    expect(record.registered).toEqual([10, 20, 23]);
    expect(record.story.get(1)).toEqual(['preparing', 'uploading', 'registering', 'done']);
  });

  it('sends each proof to its own place with the headers the API gave', async () => {
    const { api } = fakeApi();
    const put = vi.fn<(slot: ProofUploadSlot, proof: Blob) => Promise<void>>().mockResolvedValue(undefined);
    await createProofUploader({ api, renderer, put }).run('g1', queue(2), recorder().observer);
    expect(put.mock.calls.map(([slot]) => [slot.photoId, slot.headers])).toEqual([
      ['photo-1', { 'Content-Type': 'image/jpeg' }],
      ['photo-2', { 'Content-Type': 'image/jpeg' }],
    ]);
  });

  it('tells the API the real weight of each proof, not of the original photo', async () => {
    const asked: number[][] = [];
    const { api } = fakeApi();
    const spy = { ...api, requestProofUploads: (id: string, files: readonly { name: string; bytes: number }[]) => (asked.push(files.map((file) => file.bytes)), api.requestProofUploads(id, files)) };
    await createProofUploader({ api: spy, renderer, put: () => Promise.resolve() }).run('g1', queue(2), recorder().observer);
    expect(asked).toEqual([[100, 100]]);
  });

  it('marks a photo that cannot be prepared and goes on with the others', async () => {
    const { api, requests } = fakeApi();
    const flaky: ProofRenderer = { render: (file) => (file.name === 'p2.jpg' ? Promise.reject(new ProofRenderError('decode')) : renderer.render(file)) };
    const record = recorder();

    const outcome = await createProofUploader({ api, renderer: flaky, put: () => Promise.resolve() }).run('g1', queue(3), record.observer);

    expect(outcome.stopped).toBe(false);
    expect(requests).toEqual([2]);
    expect(record.done).toEqual([1, 3]);
    expect(record.story.get(2)?.at(-1)).toContain('failed: No se pudo abrir esta foto');
  });

  it('marks a photo the storage refused and registers only the ones that went up', async () => {
    const { api, registrations } = fakeApi();
    const put = vi.fn((slot: ProofUploadSlot) => (slot.photoId === 'photo-1' ? Promise.reject(new ProofUploadError(403)) : Promise.resolve()));
    const record = recorder();

    await createProofUploader({ api, renderer, put }).run('g1', queue(2), record.observer);

    expect(registrations).toEqual([['photo-2']]);
    expect(record.story.get(1)?.at(-1)).toContain('failed: El permiso para subir esta foto venció');
    expect(record.done).toEqual([2]);
  });

  it('marks the photos the API did not register, each with its reason', async () => {
    const { api } = fakeApi((ids) => registered(1, [{ photoId: ids[1] as string, reason: 'too_big' }]));
    const record = recorder();

    const outcome = await createProofUploader({ api, renderer, put: () => Promise.resolve() }).run('g1', queue(2), record.observer);

    expect(outcome.stopped).toBe(false);
    expect(record.done).toEqual([1]);
    expect(record.story.get(2)?.at(-1)).toBe('failed: La foto pesa más de lo permitido.');
  });

  it('stops when the gallery is full and leaves the rest waiting', async () => {
    const { api, requests } = fakeApi((ids) => registered(5, [{ photoId: ids[0] as string, reason: 'gallery_full' }]));
    const record = recorder();

    const outcome = await createProofUploader({ api, renderer, put: () => Promise.resolve() }).run('g1', queue(15), record.observer);

    expect(outcome).toEqual({ stopped: true, problem: 'La galería ya tiene el máximo de pruebas.' });
    expect(requests).toEqual([10]);
    expect(record.story.get(11)).toBeUndefined();
  });

  it('stops when the places cannot be asked for, marks the batch and leaves the next ones waiting', async () => {
    const { api } = fakeApi();
    const failing = { ...api, requestProofUploads: () => Promise.reject(new ApiError(409, 'closed', 'gallery.invalid_state')) };
    const record = recorder();

    const outcome = await createProofUploader({ api: failing, renderer, put: () => Promise.resolve() }).run('g1', queue(12), record.observer);

    expect(outcome).toEqual({ stopped: true, problem: 'Las pruebas ya no se pueden cambiar: el cliente ya puede elegir.' });
    expect(record.story.get(1)?.at(-1)).toContain('failed: Las pruebas ya no se pueden cambiar');
    expect(record.story.get(11)).toBeUndefined();
  });

  it('stops without a connection when registering, and marks what went up as failed so it is tried again', async () => {
    const { api } = fakeApi();
    const offline = { ...api, registerProofs: () => Promise.reject(new NetworkError()) };
    const record = recorder();

    const outcome = await createProofUploader({ api: offline, renderer, put: () => Promise.resolve() }).run('g1', queue(2), record.observer);

    expect(outcome.stopped).toBe(true);
    expect(outcome.problem).toBe('No hay conexión. Revisa tu internet y vuelve a intentarlo.');
    expect(record.story.get(1)?.at(-1)).toContain('failed');
    expect(record.done).toEqual([]);
  });

  it('does nothing and says it stopped when the run was cancelled', async () => {
    const { api, requests } = fakeApi();
    const abort = new AbortController();
    abort.abort();

    const outcome = await createProofUploader({ api, renderer, put: () => Promise.resolve() }).run('g1', queue(3), recorder().observer, abort.signal);

    expect(outcome).toEqual({ stopped: true, problem: null });
    expect(requests).toEqual([]);
  });
});
