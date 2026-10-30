import type { PendingCode } from '../types/utils/studio.types';

/** Key under which the page remembers that a code is waiting. It lives in the tab only (`sessionStorage`) and holds nothing secret. */
const KEY = 'photosense.studio.pending-code';

/**
 * Reads what the page remembers about a code that is waiting. Storage that is blocked, or text that is not what the page wrote,
 * counts as "nothing waiting": the person just starts at the password.
 * @param storage Where it is kept; the tab storage of the browser by default.
 * @returns The pending code, or null.
 */
export function readPendingCode(storage: Pick<Storage, 'getItem'> = window.sessionStorage): PendingCode | null {
  try {
    const raw = storage.getItem(KEY);
    if (raw === null) {
      return null;
    }

    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed === 'object' && parsed !== null && 'maskedEmail' in parsed && typeof parsed.maskedEmail === 'string') {
      return { maskedEmail: parsed.maskedEmail };
    }

    return null;
  } catch {
    return null;
  }
}

/**
 * Remembers that a code is waiting, so a page that reloads continues at the code. Storage that is blocked is ignored.
 * @param pending What to remember.
 * @param storage Where to keep it; the tab storage of the browser by default.
 */
export function writePendingCode(pending: PendingCode, storage: Pick<Storage, 'setItem'> = window.sessionStorage): void {
  try {
    storage.setItem(KEY, JSON.stringify(pending));
  } catch {
    // Without storage the page still works: a reload starts again at the password.
  }
}

/**
 * Forgets that a code is waiting. Storage that is blocked is ignored.
 * @param storage Where it is kept; the tab storage of the browser by default.
 */
export function clearPendingCode(storage: Pick<Storage, 'removeItem'> = window.sessionStorage): void {
  try {
    storage.removeItem(KEY);
  } catch {
    // Nothing to forget where nothing can be kept.
  }
}
