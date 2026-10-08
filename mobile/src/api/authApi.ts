import type { AuthSessionResponse } from '../types/api/auth';
import type { AuthApi } from '../types/api/authApi';
import type { HttpClient } from '../types/api/http';
import { expectRecord, readString } from './guards';

/**
 * Parses a session response, validating every field against the backend contract.
 * @param value Unknown response body.
 * @returns The typed session.
 */
export function parseAuthSession(value: unknown): AuthSessionResponse {
  const path = 'session';
  const record = expectRecord(value, path);
  return {
    accessToken: readString(record, 'accessToken', path),
    accessTokenExpiresAt: readString(record, 'accessTokenExpiresAt', path),
    refreshToken: readString(record, 'refreshToken', path),
    refreshTokenExpiresAt: readString(record, 'refreshTokenExpiresAt', path),
    photographerId: readString(record, 'photographerId', path),
    email: readString(record, 'email', path),
  };
}

/**
 * Creates the authentication API on top of an HTTP client. The client must not manage a session itself: these endpoints are
 * the ones that create and renew it.
 * @param client HTTP client without session handling.
 * @returns The authentication API.
 */
export function createAuthApi(client: HttpClient): AuthApi {
  return {
    login: (email, password, signal) => client.post('/api/auth/login', { email, password }, parseAuthSession, signal),
    refresh: (refreshToken, signal) => client.post('/api/auth/refresh', { refreshToken }, parseAuthSession, signal),
    logout: (refreshToken, signal) =>
      client.post('/api/auth/logout', { refreshToken }, () => undefined, signal),
  };
}
