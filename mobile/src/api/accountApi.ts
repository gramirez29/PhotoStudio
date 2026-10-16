import type { AccountApi } from '../types/api/accountApi';
import type { HttpClient } from '../types/api/http';
import { parseAuthSession } from './authApi';

/**
 * Creates the account API on top of an HTTP client that sends the access token of the session.
 * @param client HTTP client with session handling.
 * @returns The account API.
 */
export function createAccountApi(client: HttpClient): AccountApi {
  return {
    changePassword: (request, signal) => client.post('/api/account/password', request, parseAuthSession, signal),
  };
}
