/**
 * Where the app is in its sign-in lifecycle.
 * - `restoring`: looking for a saved session at startup.
 * - `offline`: a session is saved but the backend could not be reached to renew it.
 * - `signedOut`: no session; the login screen is shown.
 * - `signedIn`: there is a session; the app is shown.
 */
export type SessionStatus = 'restoring' | 'offline' | 'signedOut' | 'signedIn';

/** The signed-in session as the app keeps it in memory. The refresh token is not here: it lives in secure storage. */
export interface ActiveSession {
  /** Access token sent with every request. */
  readonly accessToken: string;
  /** Instant the access token stops being accepted, in milliseconds since the epoch. */
  readonly accessTokenExpiresAt: number;
  /** Photographer (tenant) identifier. */
  readonly photographerId: string;
  /** Username of the user. */
  readonly username: string;
  /** Email of the user; empty for users created before the email existed. */
  readonly email: string;
  /** Display name of the user. */
  readonly name: string;
}

/** State and setters of the session store. Side effects (network, storage) live in the session manager, not here. */
export interface SessionStore {
  /** Where the app is in its sign-in lifecycle. */
  readonly status: SessionStatus;
  /** The signed-in session, or null when there is none. */
  readonly session: ActiveSession | null;
  /** Records a session and marks the app as signed in. */
  readonly setSignedIn: (session: ActiveSession) => void;
  /** Drops the session and marks the app as signed out. */
  readonly setSignedOut: () => void;
  /** Marks the app as waiting for the backend to renew a saved session. */
  readonly setOffline: () => void;
  /** Marks the app as looking for a saved session. */
  readonly setRestoring: () => void;
}
