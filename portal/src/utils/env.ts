/** Address of the API used when `VITE_API_URL` is not set: the one of the local backend. */
export const DEFAULT_API_URL = 'http://localhost:8080';

/** Environments that are not production. Production is the default and shows no mark. */
export const TEST_ENVIRONMENTS = ['develop'] as const;

/** An environment the page labels as a test one. */
export type TestEnvironment = (typeof TEST_ENVIRONMENTS)[number];

/**
 * Normalizes the address of the API: trimmed and without trailing slashes.
 * @param raw Value of the environment variable, if any.
 * @returns The address, or the local one when the variable is missing or blank.
 */
export function normalizeApiUrl(raw: string | undefined): string {
  const value = raw?.trim();
  return value === undefined || value.length === 0 ? DEFAULT_API_URL : value.replace(/\/+$/, '');
}

/**
 * Reads the name of a test environment. Only the names in {@link TEST_ENVIRONMENTS} are accepted; anything else (missing,
 * blank, "production", a typo) means production and shows no mark, so the real site can never be labelled as a test one by mistake.
 * @param raw Value of `VITE_ENVIRONMENT`, if any.
 * @returns The test environment, or null for production.
 */
export function normalizeEnvironment(raw: string | undefined): TestEnvironment | null {
  const value = raw?.trim().toLowerCase();
  return TEST_ENVIRONMENTS.find((environment) => environment === value) ?? null;
}
