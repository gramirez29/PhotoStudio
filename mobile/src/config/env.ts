import { normalizeApiUrl, normalizePhotographerId } from '../utils/envNormalizers';

/**
 * Runtime configuration of the app, resolved once at startup from `EXPO_PUBLIC_*` variables.
 * `photographerId` is a stopgap until photographer authentication exists: the identifier is not a secret.
 */
export const env = {
  apiUrl: normalizeApiUrl(process.env.EXPO_PUBLIC_API_URL),
  photographerId: normalizePhotographerId(process.env.EXPO_PUBLIC_PHOTOGRAPHER_ID),
} as const;
