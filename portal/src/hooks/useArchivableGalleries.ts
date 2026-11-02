import { useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query';
import { studioApi } from '../api/client';
import { ApiError } from '../api/httpClient';
import type { StudioGallery } from '../types/api/studio';
import { STUDIO_DATA_PREFIX, STUDIO_SESSION_KEY } from './useStudioSession';

/** Key of the galleries whose space can be freed. */
export const ARCHIVABLE_KEY = [...STUDIO_DATA_PREFIX, 'archivable'] as const;

/**
 * Loads the galleries whose space the photographer can free: the delivered ones, the one delivered longest ago first. If the server
 * answers that the session is over (a 401), the cached session is cleared, which shows the sign-in.
 * @returns The TanStack Query result.
 */
export function useArchivableGalleries(): UseQueryResult<readonly StudioGallery[], Error> {
  const queryClient = useQueryClient();

  return useQuery({
    queryKey: ARCHIVABLE_KEY,
    queryFn: async ({ signal }) => {
      try {
        return await studioApi.listArchivable(signal);
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          queryClient.setQueryData(STUDIO_SESSION_KEY, null);
        }

        throw error;
      }
    },
  });
}
