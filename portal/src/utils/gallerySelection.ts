import type { Money, PortalGallery } from '../types/api/portal';
import type { ChoiceSummary, ChoiceWords } from '../types/utils/gallerySelection.types';
import { formatMoney } from './format';

/** Prefix of the key under which the choice in progress is remembered; the token of the link follows. */
const DRAFT_PREFIX = 'photosense.gallery.draft.';

/**
 * Adds a photo to the choice, or takes it out when it was already there. The order of choosing is kept.
 * @param chosen The photos picked so far.
 * @param photoId The photo the client tapped.
 * @returns The new choice.
 */
export function toggleChoice(chosen: readonly string[], photoId: string): string[] {
  return chosen.includes(photoId) ? chosen.filter((id) => id !== photoId) : [...chosen, photoId];
}

/**
 * Works out what the choice means: how many included photos are left, and how many photos are extra and what they cost. The server
 * counts them again when the selection is sent; this only tells the client what to expect before they send it.
 * @param chosen Number of photos picked.
 * @param gallery The gallery, which holds the terms.
 * @returns The summary.
 */
export function summarizeChoice(chosen: number, gallery: Pick<PortalGallery, 'selectablePhotos' | 'extraPhotoPrice'>): ChoiceSummary {
  const extraPhotos = Math.max(0, chosen - gallery.selectablePhotos);
  return {
    chosen,
    selectable: gallery.selectablePhotos,
    remaining: Math.max(0, gallery.selectablePhotos - chosen),
    extraPhotos,
    extraTotal: { amount: gallery.extraPhotoPrice.amount * extraPhotos, currency: gallery.extraPhotoPrice.currency },
  };
}

/**
 * Remembers the choice in progress, so a page that reloads, or a verification that expires, does not lose it. It holds only photo
 * identifiers, nothing secret, and lives in the tab. Storage that is blocked is ignored.
 * @param token Token of the link, which tells one booking from another.
 * @param chosen The photos picked so far.
 * @param storage Where to keep it; the tab storage of the browser by default.
 */
export function saveDraft(token: string, chosen: readonly string[], storage: Pick<Storage, 'setItem' | 'removeItem'> = window.sessionStorage): void {
  try {
    if (chosen.length === 0) {
      storage.removeItem(DRAFT_PREFIX + token);
    } else {
      storage.setItem(DRAFT_PREFIX + token, JSON.stringify(chosen));
    }
  } catch {
    // Without storage the choice still works; it is only lost on a reload.
  }
}

/**
 * Reads the choice in progress. Only photos that the gallery still has are kept, and a text that is not what the page wrote
 * counts as "nothing chosen".
 * @param token Token of the link.
 * @param knownIds Identifiers of the photos the gallery has.
 * @param storage Where it is kept; the tab storage of the browser by default.
 * @returns The photos picked, in the order they were picked.
 */
export function readDraft(token: string, knownIds: readonly string[], storage: Pick<Storage, 'getItem'> = window.sessionStorage): string[] {
  try {
    const raw = storage.getItem(DRAFT_PREFIX + token);
    const parsed: unknown = raw === null ? [] : JSON.parse(raw);
    if (!Array.isArray(parsed)) {
      return [];
    }

    const known = new Set(knownIds);
    return [...new Set(parsed.filter((id): id is string => typeof id === 'string' && known.has(id)))];
  } catch {
    return [];
  }
}

/**
 * Forgets the choice in progress, once it was sent.
 * @param token Token of the link.
 * @param storage Where it is kept; the tab storage of the browser by default.
 */
export function clearDraft(token: string, storage: Pick<Storage, 'removeItem'> = window.sessionStorage): void {
  try {
    storage.removeItem(DRAFT_PREFIX + token);
  } catch {
    // Nothing to forget without storage.
  }
}

/**
 * Says in words where the choice is: how many included photos are left, what the extra photos cost, and the invitation to add one
 * when the package is used up. The price is always in the words, never in small print.
 * @param summary What the choice means.
 * @param price Price of each extra photo.
 * @returns The three lines; each is null when it does not apply.
 */
export function describeChoice(summary: ChoiceSummary, price: Money): ChoiceWords {
  const each = formatMoney(price);
  const headline =
    summary.selectable === 0
      ? `Elegiste ${summary.chosen} ${summary.chosen === 1 ? 'foto' : 'fotos'}`
      : summary.remaining > 0
        ? `Te quedan ${summary.remaining} de ${summary.selectable} ${summary.selectable === 1 ? 'foto incluida' : 'fotos incluidas'}`
        : `Ya elegiste las ${summary.selectable} ${summary.selectable === 1 ? 'foto' : 'fotos'} de tu paquete`;

  return {
    headline,
    extra:
      summary.extraPhotos > 0
        ? `${summary.extraPhotos} ${summary.extraPhotos === 1 ? 'foto adicional' : 'fotos adicionales'} × ${each} = ${formatMoney(summary.extraTotal)}. Se suma a tu saldo.`
        : null,
    invitation:
      summary.remaining === 0
        ? `Las fotos adicionales cuestan ${each} cada una. Si hay otra que te encante, ¡puedes sumarla!`
        : null,
  };
}
