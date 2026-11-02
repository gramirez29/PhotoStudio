import { useCallback, useState } from 'react';
import type { PortalSession } from '../types/api/portal';
import type { PersistedSession } from '../types/hooks/usePersistedSession.types';
import { forgetSession, readStoredSession, storeSession } from '../utils/portalSession';

/**
 * Keeps the session of a link for the page and remembers it in the browser. The client confirms their email once, with the first code,
 * and that confirmation serves for the contract, the payments and the gallery, in this visit and in the next ones, until it expires or
 * the client closes it. A session that stops working (the server refuses it) is set to null by the flows, which forgets it here too.
 * @param token Secret token taken from the URL.
 * @returns The session and the setter that remembers or forgets it.
 */
export function usePersistedSession(token: string): PersistedSession {
  const [session, setSession] = useState<PortalSession | null>(() => readStoredSession(token, Date.now()));

  const change = useCallback(
    (next: PortalSession | null): void => {
      if (next === null) {
        forgetSession(token);
      } else {
        storeSession(token, next);
      }

      setSession(next);
    },
    [token],
  );

  return [session, change];
}
