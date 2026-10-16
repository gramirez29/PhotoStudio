/** Address of the API used when `VITE_API_URL` is not set: the one of the local backend. */
export const DEFAULT_API_URL = 'http://localhost:8080';

/**
 * Normalizes the address of the API: trimmed and without trailing slashes.
 * @param raw Value of the environment variable, if any.
 * @returns The address, or the local one when the variable is missing or blank.
 */
export function normalizeApiUrl(raw: string | undefined): string {
  const value = raw?.trim();
  return value === undefined || value.length === 0 ? DEFAULT_API_URL : value.replace(/\/+$/, '');
}
