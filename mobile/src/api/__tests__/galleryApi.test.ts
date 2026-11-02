import { createGalleryApi, parseFinalDownload, parseGallery, parseProofs, parseSelection } from '../galleryApi';
import { galleryPayload, selectionPayload } from '../__fixtures__/galleryPayloads';
import type { HttpClient } from '../../types/api/http';

describe('parseGallery', () => {
  it('reads a gallery with everything the photographer needs', () => {
    const gallery = parseGallery(galleryPayload);

    expect(gallery).toMatchObject({ status: 'InEditing', selectablePhotos: 2, selectedPhotoCount: 3, extraPhotoCount: 1, zip: null });
    expect(gallery.allowedActions).toEqual(['UploadFinals', 'ReopenSelection']);
    expect(gallery.extraPhotoPrice).toEqual({ amount: 3000, currency: 'CRC' });
  });

  it('reads the ZIP and the reopening when there are', () => {
    const gallery = parseGallery({
      ...galleryPayload,
      zip: { bytes: 220_000_000, uploadedAt: '2026-11-03T14:00:00+00:00', downloads: 2, lastDownloadedAt: null },
      selectionReopenedAt: '2026-11-04T12:00:00+00:00',
      selectionReopenReason: 'cambio de fotos',
    });

    expect(gallery.zip).toEqual({ bytes: 220_000_000, uploadedAt: '2026-11-03T14:00:00+00:00', downloads: 2, lastDownloadedAt: null });
    expect(gallery.selectionReopenReason).toBe('cambio de fotos');
  });

  it('keeps only the actions this version knows, so a new one does not break the screen', () => {
    expect(parseGallery({ ...galleryPayload, allowedActions: ['ReopenSelection', 'SomethingNew'] }).allowedActions).toEqual(['ReopenSelection']);
  });

  it('refuses a gallery with the wrong shape or an unknown state', () => {
    expect(() => parseGallery({ ...galleryPayload, status: 'Mystery' })).toThrow();
    expect(() => parseGallery({ ...galleryPayload, selectedPhotoCount: 'many' })).toThrow();
    expect(() => parseGallery(null)).toThrow();
  });
});

describe('parseSelection', () => {
  it('reads which photos are extra and what is known about each final, with its small image', () => {
    const selection = parseSelection(selectionPayload);

    expect(selection.photos.map((photo) => photo.isExtra)).toEqual([false, false, true]);
    expect(selection.photos[0]).toMatchObject({ finalBytes: 4_404_019, finalWidth: 1250, finalHeight: 3000, finalDownloads: 2, thumbIsFinal: true });
    expect(selection.photos[0]?.thumbUrl).toContain('thumbs/p1.jpg');
    expect(selection.photos[1]).toMatchObject({ finalBytes: null, finalWidth: null, finalDownloads: 0, thumbIsFinal: false });
  });

  it('refuses a photo without a name', () => {
    expect(() => parseSelection({ ...selectionPayload, photos: [{ id: 'p1' }] })).toThrow();
  });
});

describe('parseProofs and parseFinalDownload', () => {
  it('read the proofs with their address and the address of a final', () => {
    expect(parseProofs({ items: [{ id: 'p1', name: 'a.jpg', bytes: 10, url: 'https://s/1', expiresAt: 'x' }] })).toEqual([{ id: 'p1', name: 'a.jpg', bytes: 10, url: 'https://s/1', expiresAt: 'x' }]);
    expect(parseFinalDownload({ fileName: 'a.jpg', url: 'https://s/f', expiresAt: 'x' })).toEqual({ fileName: 'a.jpg', url: 'https://s/f', expiresAt: 'x' });
    expect(() => parseProofs({ items: [{ id: 1 }] })).toThrow();
  });
});

describe('createGalleryApi', () => {
  /**
   * Builds an HTTP client that answers every request with the given body and records what was asked.
   * @param body What to answer.
   * @returns The client and the requests.
   */
  function fakeClient(body: unknown): { readonly client: HttpClient; readonly calls: { method: string; path: string; body?: unknown }[] } {
    const calls: { method: string; path: string; body?: unknown }[] = [];
    const client: HttpClient = {
      get: (path, parse) => {
        calls.push({ method: 'GET', path });
        return Promise.resolve(parse(body));
      },
      post: (path, sent, parse) => {
        calls.push({ method: 'POST', path, body: sent });
        return Promise.resolve(parse(body));
      },
    };
    return { client, calls };
  }

  it('calls the endpoints of the app with the token of the session, one for each thing the photographer does', async () => {
    const { client, calls } = fakeClient(galleryPayload);
    const api = createGalleryApi(client);

    await api.getByBooking('b 1');
    await api.get('g1');
    await api.editTerms('g1', { includedPhotos: 30, bonusPhotos: 5, extraPhotoPrice: 3000 });
    await api.removeProof('g1', 'p1');
    await api.openSelection('g1');
    await api.reopenSelection('g1', 'cambio');
    await api.publishFinals('g1');
    await api.releaseExtras('g1');
    await api.removeZip('g1');

    expect(calls).toEqual([
      { method: 'GET', path: '/api/galleries/by-booking/b%201' },
      { method: 'GET', path: '/api/galleries/g1' },
      { method: 'POST', path: '/api/galleries/g1/terms', body: { includedPhotos: 30, bonusPhotos: 5, extraPhotoPrice: 3000 } },
      { method: 'POST', path: '/api/galleries/g1/proofs/p1/remove', body: {} },
      { method: 'POST', path: '/api/galleries/g1/selection/open', body: {} },
      { method: 'POST', path: '/api/galleries/g1/selection/reopen', body: { reason: 'cambio' } },
      { method: 'POST', path: '/api/galleries/g1/finals/publish', body: {} },
      { method: 'POST', path: '/api/galleries/g1/extras/release', body: {} },
      { method: 'POST', path: '/api/galleries/g1/zip/remove', body: {} },
    ]);
  });

  it('reads the selection, the proofs and the address of a final', async () => {
    expect((await createGalleryApi(fakeClient(selectionPayload).client).getSelection('g1')).photos).toHaveLength(3);
    expect(await createGalleryApi(fakeClient({ items: [] }).client).listProofs('g1')).toEqual([]);
    const { client, calls } = fakeClient({ fileName: 'a.jpg', url: 'https://s/f', expiresAt: 'x' });
    await createGalleryApi(client).getFinalDownload('g1', 'p 1');
    expect(calls[0]?.path).toBe('/api/galleries/g1/finals/p%201/download');
  });
});
