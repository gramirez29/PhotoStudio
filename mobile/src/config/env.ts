/** API URL used when `EXPO_PUBLIC_API_URL` is not defined (local backend). */
export const DEFAULT_API_URL = 'http://localhost:8080';

/**
 * Normalizes the API base URL: trims it, falls back to {@link DEFAULT_API_URL} and removes trailing slashes.
 * @param raw Value of the environment variable, if any.
 * @returns The base URL without a trailing slash.
 */
export function normalizeApiUrl(raw: string | undefined): string {
  const value = raw?.trim();
  if (value === undefined || value.length === 0) {
    return DEFAULT_API_URL;
  }

  return value.replace(/\/+$/, '');
}

/** Runtime configuration of the app, resolved once at startup from `EXPO_PUBLIC_*` variables. */
export const env = {
  apiUrl: normalizeApiUrl(process.env.EXPO_PUBLIC_API_URL),
} as const;
