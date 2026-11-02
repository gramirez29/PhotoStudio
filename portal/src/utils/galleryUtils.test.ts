import { describe, expect, it } from 'vitest';
import { parseGallerySummary, parsePortalGallery } from '../api/portalApi';
import { parseStudioSelection } from '../api/studioApi';
import { describeGalleryCard } from './galleryCard';
import { galleryFlowReducer, INITIAL_GALLERY_FLOW } from './galleryFlow';
import { clearDraft, describeChoice, readDraft, saveDraft, summarizeChoice, toggleChoice } from './gallerySelection';

const PRICE = { amount: 3000, currency: 'CRC' };

/**
 * Builds a fake of the storage of the tab.
 * @returns The storage and what it holds.
 */
function fakeStorage(): Pick<Storage, 'getItem' | 'setItem' | 'removeItem'> & { readonly items: Map<string, string> } {
  const items = new Map<string, string>();
  return {
    items,
    getItem: (key) => items.get(key) ?? null,
    setItem: (key, value) => void items.set(key, value),
    removeItem: (key) => void items.delete(key),
  };
}

describe('toggleChoice', () => {
  it('adds a photo at the end and takes it out when it is tapped again', () => {
    expect(toggleChoice(['a'], 'b')).toEqual(['a', 'b']);
    expect(toggleChoice(['a', 'b', 'c'], 'b')).toEqual(['a', 'c']);
    expect(toggleChoice([], 'a')).toEqual(['a']);
  });
});

describe('summarizeChoice', () => {
  it('counts the included photos left and no extra while the package is not used up', () => {
    expect(summarizeChoice(3, { selectablePhotos: 15, extraPhotoPrice: PRICE })).toEqual({
      chosen: 3,
      selectable: 15,
      remaining: 12,
      extraPhotos: 0,
      extraTotal: { amount: 0, currency: 'CRC' },
    });
  });

  it('counts the photos beyond the package as extra, at the price of the gallery', () => {
    const summary = summarizeChoice(17, { selectablePhotos: 15, extraPhotoPrice: PRICE });
    expect(summary.remaining).toBe(0);
    expect(summary.extraPhotos).toBe(2);
    expect(summary.extraTotal).toEqual({ amount: 6000, currency: 'CRC' });
  });

  it('treats a package with no photos as all extra', () => {
    expect(summarizeChoice(2, { selectablePhotos: 0, extraPhotoPrice: PRICE }).extraTotal.amount).toBe(6000);
  });
});

describe('describeChoice', () => {
  it('says how many are left, without the invitation yet', () => {
    const words = describeChoice(summarizeChoice(3, { selectablePhotos: 15, extraPhotoPrice: PRICE }), PRICE);
    expect(words).toEqual({ headline: 'Te quedan 12 de 15 fotos incluidas', extra: null, invitation: null });
  });

  it('says the package is used up and invites to add one, with the price in the words', () => {
    const words = describeChoice(summarizeChoice(15, { selectablePhotos: 15, extraPhotoPrice: PRICE }), PRICE);
    expect(words.headline).toBe('Ya elegiste las 15 fotos de tu paquete');
    expect(words.invitation).toMatch(/cuestan .*3.*000.* cada una\. Si hay otra que te encante, ¡puedes sumarla!/);
    expect(words.extra).toBeNull();
  });

  it('says what the extra photos cost, in the singular and the plural', () => {
    expect(describeChoice(summarizeChoice(16, { selectablePhotos: 15, extraPhotoPrice: PRICE }), PRICE).extra).toMatch(/^1 foto adicional × .* = .*Se suma a tu saldo\.$/);
    expect(describeChoice(summarizeChoice(18, { selectablePhotos: 15, extraPhotoPrice: PRICE }), PRICE).extra).toMatch(/^3 fotos adicionales × /);
  });

  it('words a package of one photo and one with none', () => {
    expect(describeChoice(summarizeChoice(0, { selectablePhotos: 1, extraPhotoPrice: PRICE }), PRICE).headline).toBe('Te quedan 1 de 1 foto incluida');
    expect(describeChoice(summarizeChoice(1, { selectablePhotos: 0, extraPhotoPrice: PRICE }), PRICE).headline).toBe('Elegiste 1 foto');
  });
});

describe('the draft of the choice', () => {
  it('is saved, read back in order with only the photos the gallery still has, and forgotten', () => {
    const storage = fakeStorage();
    saveDraft('token', ['b', 'x', 'a', 'b'], storage);

    expect(readDraft('token', ['a', 'b', 'c'], storage)).toEqual(['b', 'a']);
    expect(readDraft('other-token', ['a', 'b'], storage)).toEqual([]);
    clearDraft('token', storage);
    expect(readDraft('token', ['a', 'b'], storage)).toEqual([]);
  });

  it('forgets an empty choice instead of keeping an empty list', () => {
    const storage = fakeStorage();
    saveDraft('token', ['a'], storage);
    saveDraft('token', [], storage);

    expect(storage.items.size).toBe(0);
  });

  it('reads nothing from text that is not what the page wrote, and survives a storage that is blocked', () => {
    const storage = fakeStorage();
    storage.items.set('photosense.gallery.draft.token', '{"not":"a list"}');
    expect(readDraft('token', ['a'], storage)).toEqual([]);
    storage.items.set('photosense.gallery.draft.token', 'not json');
    expect(readDraft('token', ['a'], storage)).toEqual([]);

    const blocked = {
      getItem: (): string | null => {
        throw new Error('blocked');
      },
      setItem: (): void => {
        throw new Error('blocked');
      },
      removeItem: (): void => {
        throw new Error('blocked');
      },
    };
    expect(readDraft('token', ['a'], blocked)).toEqual([]);
    expect(() => saveDraft('token', ['a'], blocked)).not.toThrow();
    expect(() => clearDraft('token', blocked)).not.toThrow();
  });
});

describe('the flow of the gallery', () => {
  const session = { accessToken: 'jwt', expiresAt: new Date(Date.now() + 30 * 60_000).toISOString() };
  const stale = { accessToken: 'jwt', expiresAt: new Date(Date.now() + 10_000).toISOString() };

  it('asks for the code when there is no session, or one about to expire, and skips it when there is a good one', () => {
    expect(galleryFlowReducer(INITIAL_GALLERY_FLOW, { type: 'open', now: Date.now(), session: null }).step).toBe('request');
    expect(galleryFlowReducer(INITIAL_GALLERY_FLOW, { type: 'open', now: Date.now(), session: stale }).step).toBe('request');
    const open = galleryFlowReducer(INITIAL_GALLERY_FLOW, { type: 'open', now: Date.now(), session });
    expect(open).toMatchObject({ step: 'choose', session });
  });

  it('goes from the code to the gallery, and back to the code when the session expires, remembering where the code went', () => {
    const sent = galleryFlowReducer({ ...INITIAL_GALLERY_FLOW, step: 'request' }, { type: 'codeSent', maskedEmail: 'm***@example.com' });
    expect(sent).toMatchObject({ step: 'enter', maskedEmail: 'm***@example.com' });
    const verified = galleryFlowReducer(sent, { type: 'verified', session });
    expect(verified).toMatchObject({ step: 'choose', session });
    expect(galleryFlowReducer(verified, { type: 'sessionExpired' })).toMatchObject({ step: 'request', session: null, maskedEmail: 'm***@example.com' });
    expect(galleryFlowReducer(verified, { type: 'close' }).step).toBe('closed');
  });
});

describe('describeGalleryCard', () => {
  it('words each state the client can see, and nothing for a gallery that is not ready', () => {
    expect(describeGalleryCard('InSelection')?.action).toBe('Ver mis fotos y elegir');
    expect(describeGalleryCard('InEditing')?.text).toMatch(/Ya enviaste tu selección/);
    expect(describeGalleryCard('Delivered')?.action).toBe('Ver mi selección');
    expect(describeGalleryCard('Archived')?.action).toBeNull();
    expect(describeGalleryCard('Pending')).toBeNull();
  });
});

describe('the answers of the gallery', () => {
  const photo = { id: 'p1', url: 'https://s/1', expiresAt: '2026-10-30T12:20:00+00:00' };

  it('parses the summary and refuses a state it does not know', () => {
    expect(parseGallerySummary({ status: 'InSelection' })).toEqual({ status: 'InSelection' });
    expect(() => parseGallerySummary({ status: 'Mystery' })).toThrow();
  });

  it('parses the gallery with and without a selection', () => {
    const open = parsePortalGallery({ status: 'InSelection', selectablePhotos: 15, extraPhotoPrice: PRICE, photos: [photo], selection: null });
    expect(open.selection).toBeNull();
    expect(open.photos).toEqual([photo]);

    const sent = parsePortalGallery({
      status: 'InEditing',
      selectablePhotos: 15,
      extraPhotoPrice: PRICE,
      photos: [photo],
      selection: { photoIds: ['p1'], submittedAt: '2026-10-30T12:00:00+00:00', extraPhotos: 0, extraTotal: { amount: 0, currency: 'CRC' } },
    });
    expect(sent.selection).toMatchObject({ photoIds: ['p1'], extraPhotos: 0 });
  });

  it('refuses a gallery with the wrong shape instead of guessing', () => {
    expect(() => parsePortalGallery({ status: 'InSelection', selectablePhotos: 'many', extraPhotoPrice: PRICE, photos: [], selection: null })).toThrow();
    expect(() => parsePortalGallery({ status: 'InSelection', selectablePhotos: 1, extraPhotoPrice: PRICE, photos: [{ id: 3 }], selection: null })).toThrow();
    expect(() =>
      parsePortalGallery({ status: 'InEditing', selectablePhotos: 1, extraPhotoPrice: PRICE, photos: [], selection: { photoIds: [7], submittedAt: 'x', extraPhotos: 0, extraTotal: PRICE } }),
    ).toThrow();
  });

  it('parses what the photographer sees of the selection', () => {
    expect(parseStudioSelection({ submittedAt: null, extraPhotos: 0, extraTotal: { amount: 0, currency: 'CRC' }, photos: [] })).toEqual({
      submittedAt: null,
      extraPhotos: 0,
      extraTotal: { amount: 0, currency: 'CRC' },
      photos: [],
    });
    expect(
      parseStudioSelection({ submittedAt: '2026-10-30T12:00:00+00:00', extraPhotos: 1, extraTotal: PRICE, photos: [{ id: 'p1', name: 'IMG_0001.jpg' }] }).photos,
    ).toEqual([{ id: 'p1', name: 'IMG_0001.jpg' }]);
  });
});
