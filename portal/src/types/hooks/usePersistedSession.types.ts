import type { PortalSession } from '../api/portal';

/** The session of a link and the way to change it: the session now, and a setter that remembers or forgets it in the browser. */
export type PersistedSession = readonly [session: PortalSession | null, setSession: (session: PortalSession | null) => void];
