import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { studioApi } from '../api/client';
import { ApiError } from '../api/httpClient';
import type { StudioUser } from '../types/api/studio';

/** Key of the cached session of the web area. */
export const STUDIO_SESSION_KEY = ['studio', 'session'] as const;

/** Prefix of every key that holds data of the signed-in photographer, so signing out can drop all of it. */
export const STUDIO_DATA_PREFIX = ['studio', 'galleries'] as const;

/**
 * Asks the server who is signed in. "Nobody" is not a failure here: a 401 gives <c>null</c>, which is what shows the sign-in. A
 * session is checked again when the person comes back to the tab, so one that ended while they were away shows the sign-in and not
 * a page that no longer works.
 * @returns The TanStack Query result: the user, <c>null</c> when nobody is signed in, or an error when the server could not be asked.
 */
export function useStudioSession(): UseQueryResult<StudioUser | null, Error> {
  return useQuery({
    queryKey: STUDIO_SESSION_KEY,
    queryFn: async ({ signal }) => {
      try {
        return await studioApi.getSession(signal);
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          return null;
        }

        throw error;
      }
    },
  });
}
