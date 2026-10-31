import { describe, expect, it, vi } from 'vitest';
import { galleryPayload, jsonResponse } from '../test/fixtures';
import type { ProofUploadSlot } from '../types/api/proofs';
import { ApiError, NetworkError, createHttpClient } from './httpClient';
import { ProofUploadError, putProof } from './proofStorage';
import { createStudioApi, parseProofList, parseProofSlots, parseRegisterProofs } from './studioApi';

const slot: ProofUploadSlot = {
  photoId: 'photo-1',
  url: 'https://account.r2.cloudflarestorage.com/bucket/galleries/g/proofs/p.jpg?X-Amz-Signature=secret',
  headers: { 'Content-Type': 'image/jpeg' },
  expiresAt: '2026-10-30T12:15:00+00:00',
};

describe('the answers about proofs', () => {
  it('parses the places to upload', () => {
    expect(parseProofSlots({ slots: [{ photoId: 'p1', url: 'https://s/1', headers: { 'Content-Type': 'image/jpeg' }, expiresAt: '2026-10-30T12:15:00+00:00' }] })).toEqual([
      { photoId: 'p1', url: 'https://s/1', headers: { 'Content-Type': 'image/jpeg' }, expiresAt: '2026-10-30T12:15:00+00:00' },
    ]);
  });

  it('parses the outcome of registering, with the gallery and the rejections', () => {
    const result = parseRegisterProofs({ gallery: { ...galleryPayload, photoCount: 3 }, rejected: [{ photoId: 'p2', reason: 'too_big' }] });
    expect(result.gallery.photoCount).toBe(3);
    expect(result.rejected).toEqual([{ photoId: 'p2', reason: 'too_big' }]);
  });

  it('parses the list of proofs', () => {
    expect(parseProofList({ items: [{ id: 'p1', name: 'IMG_1.jpg', bytes: 1234, url: 'https://s/1', expiresAt: '2026-10-30T12:10:00+00:00' }] })).toEqual([
      { id: 'p1', name: 'IMG_1.jpg', bytes: 1234, url: 'https://s/1', expiresAt: '2026-10-30T12:10:00+00:00' },
    ]);
  });

  it('refuses an answer with the wrong shape instead of guessing', () => {
    expect(() => parseProofSlots({ slots: [{ photoId: 'p1' }] })).toThrow();
    expect(() => parseProofList({ items: [{ id: 'p1', name: 'x', bytes: 'big', url: 'u', expiresAt: 'e' }] })).toThrow();
    expect(() => parseRegisterProofs({ gallery: { ...galleryPayload, photoCount: -1 }, rejected: [] })).toThrow();
  });
});

describe('the requests about proofs', () => {
  it('sends each to its path with its body', async () => {
    const fetchFn = vi.fn<typeof fetch>().mockImplementation(() => Promise.resolve(jsonResponse(200, { slots: [] })));
    const api = createStudioApi(createHttpClient('https://api.test', { fetchFn, withCredentials: true }));

    await api.requestProofUploads('g1', [{ name: 'a.jpg', bytes: 10 }]);
    await expect(api.registerProofs('g1', [{ photoId: 'p1', name: 'a.jpg' }])).rejects.toThrow();

    const [first, second] = fetchFn.mock.calls;
    expect(first?.[0]).toBe('https://api.test/api/web/galleries/g1/proofs/uploads');
    expect(JSON.parse(String(first?.[1]?.body))).toEqual({ files: [{ name: 'a.jpg', bytes: 10 }] });
    expect(second?.[0]).toBe('https://api.test/api/web/galleries/g1/proofs');
    expect(JSON.parse(String(second?.[1]?.body))).toEqual({ photos: [{ photoId: 'p1', name: 'a.jpg' }] });
    expect(first?.[1]?.credentials).toBe('include');
  });
});

describe('putProof', () => {
  it('sends the proof to the signed address with the headers given, and no cookies or referrer', async () => {
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 200 }));
    const proof = new Blob([new Uint8Array(5)]);

    await putProof(slot, proof, undefined, fetchFn);

    const [url, init] = fetchFn.mock.calls[0] ?? [];
    expect(url).toBe(slot.url);
    expect(init).toMatchObject({ method: 'PUT', headers: { 'Content-Type': 'image/jpeg' }, body: proof, credentials: 'omit', referrerPolicy: 'no-referrer' });
  });

  it('fails with the status when the storage refuses, and never puts the address in the message', async () => {
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(new Response('denied', { status: 403 }));
    const error = await putProof(slot, new Blob([]), undefined, fetchFn).catch((caught: unknown) => caught);
    expect(error).toBeInstanceOf(ProofUploadError);
    expect((error as ProofUploadError).status).toBe(403);
    expect((error as ProofUploadError).message).not.toContain('Signature');
  });

  it('fails as a network error when the storage cannot be reached, and lets a cancellation through', async () => {
    await expect(putProof(slot, new Blob([]), undefined, vi.fn<typeof fetch>().mockRejectedValue(new TypeError('offline')))).rejects.toBeInstanceOf(NetworkError);
    const aborted = new DOMException('aborted', 'AbortError');
    await expect(putProof(slot, new Blob([]), undefined, vi.fn<typeof fetch>().mockRejectedValue(aborted))).rejects.toBe(aborted);
  });

  it('is not confused with an error of the API', () => {
    expect(new ProofUploadError(500)).not.toBeInstanceOf(ApiError);
  });
});
