import { normalizeApiUrl } from '../utils/envNormalizers';

/**
 * Runtime configuration of the app, resolved once at startup from `EXPO_PUBLIC_*` variables.
 */
export const env = {
  apiUrl: normalizeApiUrl(process.env.EXPO_PUBLIC_API_URL),
} as const;
