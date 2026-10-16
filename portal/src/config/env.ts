import { normalizeApiUrl, normalizeEnvironment } from '../utils/env';

/** Address of the API, from `VITE_API_URL`. Public: it ends up in the bundle. */
export const apiUrl = normalizeApiUrl(import.meta.env.VITE_API_URL);

/** Test environment this build is for (`VITE_ENVIRONMENT`), or null for production. It only drives a visible mark. */
export const testEnvironment = normalizeEnvironment(import.meta.env.VITE_ENVIRONMENT);
