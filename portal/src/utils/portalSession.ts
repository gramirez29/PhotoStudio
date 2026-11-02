import type { PortalSession } from '../types/api/portal';
import { isSessionValid } from './proofFlow';

/** Prefix of the key under which the session of a link is remembered; the token of the link follows. */
const SESSION_PREFIX = 'photosense.portal.session.';

/**
 * Reads the session that was remembered for a link, if it can still be used. What is stored is checked as if it came from outside: a
 * text that is not what the page wrote, or a session that is about to expire, counts as "no session", and a storage that is blocked
 * is ignored.
 * @param token Token of the link, which tells one booking from another.
 * @param now Current instant in milliseconds since the epoch.
 * @param storage Where it is kept; the local storage of the browser by default.
 * @returns The session, or null.
 */
export function readStoredSession(token: string, now: number, storage: Pick<Storage, 'getItem' | 'removeItem'> = window.localStorage): PortalSession | null {
  try {
    const raw = storage.getItem(SESSION_PREFIX + token);
    if (raw === null) {
      return null;
    }

    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed === 'object' && parsed !== null && 'accessToken' in parsed && 'expiresAt' in parsed) {
      const { accessToken, expiresAt } = parsed;
      if (typeof accessToken === 'string' && typeof expiresAt === 'string') {
        const session: PortalSession = { accessToken, expiresAt };
        if (isSessionValid(session, now)) {
          return session;
        }
      }
    }

    storage.removeItem(SESSION_PREFIX + token);
    return null;
  } catch {
    // What is stored cannot be read: it is dropped so it is not tried again.
    forgetSession(token, storage);
    return null;
  }
}

/**
 * Remembers the session of a link in this browser, so the client does not have to confirm their email again on the next visit.
 * @param token Token of the link.
 * @param session The session to remember.
 * @param storage Where to keep it; the local storage of the browser by default.
 */
export function storeSession(token: string, session: PortalSession, storage: Pick<Storage, 'setItem'> = window.localStorage): void {
  try {
    storage.setItem(SESSION_PREFIX + token, JSON.stringify({ accessToken: session.accessToken, expiresAt: session.expiresAt }));
  } catch {
    // Without storage the session still works for this visit; it is only not remembered.
  }
}

/**
 * Forgets the session of a link in this browser.
 * @param token Token of the link.
 * @param storage Where it is kept; the local storage of the browser by default.
 */
export function forgetSession(token: string, storage: Pick<Storage, 'removeItem'> = window.localStorage): void {
  try {
    storage.removeItem(SESSION_PREFIX + token);
  } catch {
    // Nothing to forget without storage.
  }
}
