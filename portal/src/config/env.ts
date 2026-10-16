import { normalizeApiUrl } from '../utils/env';

/** Address of the API, from `VITE_API_URL`. Public: it ends up in the bundle. */
export const apiUrl = normalizeApiUrl(import.meta.env.VITE_API_URL);
