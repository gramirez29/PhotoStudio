import { authApi } from '../api/authClient';
import { ApiError } from '../api/httpClient';
import { clearRefreshToken, loadRefreshToken, saveRefreshToken } from '../storage/refreshTokenStorage';
import type { AuthSessionResponse, RegisterRequest } from '../types/api/auth';
import type { AccessTokenProvider } from '../types/api/http';
import { useSessionStore } from './sessionStore';

/** An access token this close to expiring (in milliseconds) is renewed before use, so a request never leaves with a stale one. */
export const REFRESH_MARGIN_MS = 60_000;

/** The refresh token in memory, so each renewal does not have to read the secure storage. Mirrors what is saved. */
let currentRefreshToken: string | null = null;

/** The renewal in progress, shared by every caller that needs one at the same time. */
let renewalInFlight: Promise<string | null> | null = null;

/**
 * Adopts a session returned by the backend: keeps its refresh token (in memory and in secure storage) and records the access
 * token in the store, which marks the app as signed in. The refresh token is saved before anything uses the new session
 * because the backend already revoked the previous one.
 * @param session Session returned by sign-in or by a renewal.
 * @returns A promise that resolves when the session is in place.
 */
async function adoptSession(session: AuthSessionResponse): Promise<void> {
  currentRefreshToken = session.refreshToken;
  await saveRefreshToken(session.refreshToken);
  useSessionStore.getState().setSignedIn({
    accessToken: session.accessToken,
    accessTokenExpiresAt: Date.parse(session.accessTokenExpiresAt),
    photographerId: session.photographerId,
    username: session.username,
    email: session.email,
    name: session.name,
  });
}

/**
 * Forgets the session on this device: memory, secure storage and store.
 * @returns A promise that resolves when the session is gone.
 */
async function dropSession(): Promise<void> {
  currentRefreshToken = null;
  useSessionStore.getState().setSignedOut();
  await clearRefreshToken();
}

/**
 * Exchanges the refresh token for a new session. The backend rejects the refresh token with 401 when it is unknown,
 * expired or was already used; that ends the session. Any other failure (no network, server down) is not the end of the
 * session, so it is rethrown and the saved refresh token is kept.
 * @returns The new access token, or null when the session ended.
 */
async function renewSession(): Promise<string | null> {
  const refreshToken = currentRefreshToken ?? (await loadRefreshToken());
  if (refreshToken === null) {
    await dropSession();
    return null;
  }

  try {
    const session = await authApi.refresh(refreshToken);
    await adoptSession(session);
    return session.accessToken;
  } catch (error: unknown) {
    if (error instanceof ApiError && error.status === 401) {
      await dropSession();
      return null;
    }

    throw error;
  }
}

/**
 * Renews the session. Calls made while a renewal is running share it: the refresh token is single-use, so two renewals in
 * parallel would make the second one look like a replay and the backend would revoke the whole session.
 * @returns The new access token, or null when the session ended.
 */
export function renewSessionOnce(): Promise<string | null> {
  renewalInFlight ??= renewSession().finally(() => {
    renewalInFlight = null;
  });
  return renewalInFlight;
}

/**
 * Creates an account and keeps the session it starts, so the person who just registered is already signed in.
 * @param request Data of the new user.
 * @returns A promise that resolves when the session is in place.
 * @throws ApiError when the username is taken, a field is not valid or sign-up is closed.
 */
export async function register(request: RegisterRequest): Promise<void> {
  await adoptSession(await authApi.register(request));
}

/**
 * Signs in with username and password and keeps the session.
 * @param username Username of the user.
 * @param password Password of the user.
 * @returns A promise that resolves when the session is in place.
 * @throws ApiError when the credentials are not accepted or the user is locked.
 */
export async function signIn(username: string, password: string): Promise<void> {
  await adoptSession(await authApi.login(username, password));
}

/**
 * Looks for a saved session at startup and renews it. With none, the app shows the login screen; if the backend cannot be
 * reached, the app asks to retry instead of signing the photographer out for a network problem.
 * @returns A promise that resolves when the status of the app is known.
 */
export async function restoreSession(): Promise<void> {
  const store = useSessionStore.getState();
  store.setRestoring();

  const saved = await loadRefreshToken();
  if (saved === null) {
    store.setSignedOut();
    return;
  }

  currentRefreshToken = saved;
  try {
    await renewSessionOnce();
  } catch {
    useSessionStore.getState().setOffline();
  }
}

/**
 * Ends the session. The device forgets it at once; telling the backend to revoke it is best effort, so signing out works
 * without a connection (the refresh token then simply expires on its own).
 * @returns A promise that resolves when the device has forgotten the session.
 */
export async function signOut(): Promise<void> {
  const refreshToken = currentRefreshToken ?? (await loadRefreshToken());
  await dropSession();

  if (refreshToken !== null) {
    try {
      await authApi.logout(refreshToken);
    } catch {
      // Best effort: the session is already gone on this device.
    }
  }
}

/**
 * Returns an access token that is valid for at least {@link REFRESH_MARGIN_MS}, renewing the session first when needed.
 * @returns The access token, or null when there is no session.
 */
export async function getValidAccessToken(): Promise<string | null> {
  const { session } = useSessionStore.getState();
  if (session === null) {
    return null;
  }

  if (session.accessTokenExpiresAt - Date.now() > REFRESH_MARGIN_MS) {
    return session.accessToken;
  }

  return renewSessionOnce();
}

/**
 * Handles a 401 on a request sent with `rejectedToken`. If the session already holds a different token (another request
 * renewed it meanwhile) that token is used; otherwise the session is renewed.
 * @param rejectedToken Access token the backend refused.
 * @returns A token to retry with, or null when the session ended.
 */
export function renewAfterRejection(rejectedToken: string): Promise<string | null> {
  const { session } = useSessionStore.getState();
  if (session !== null && session.accessToken !== rejectedToken) {
    return Promise.resolve(session.accessToken);
  }

  return renewSessionOnce();
}

/** Plugs the session into the HTTP client of the app. */
export const sessionTokenProvider: AccessTokenProvider = {
  getAccessToken: getValidAccessToken,
  renewAfterRejection,
};
