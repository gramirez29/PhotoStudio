import { useInfiniteQuery, useQueryClient, type InfiniteData, type UseInfiniteQueryResult } from '@tanstack/react-query';
import { studioApi } from '../api/client';
import { ApiError } from '../api/httpClient';
import type { StudioGalleryPage } from '../types/api/studio';
import { STUDIO_DATA_PREFIX, STUDIO_SESSION_KEY } from './useStudioSession';

/**
 * Loads the galleries of the signed-in photographer a page at a time, the most recently created first. If the server answers that
 * the session is over (a 401), the cached session is cleared, which shows the sign-in instead of a list that cannot load.
 * @returns The TanStack Query result, whose <c>fetchNextPage</c> loads the page after the last one.
 */
export function useStudioGalleries(): UseInfiniteQueryResult<InfiniteData<StudioGalleryPage, string | null>, Error> {
  const queryClient = useQueryClient();

  return useInfiniteQuery({
    queryKey: STUDIO_DATA_PREFIX,
    queryFn: async ({ pageParam, signal }) => {
      try {
        return await studioApi.listGalleries(pageParam, signal);
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          queryClient.setQueryData(STUDIO_SESSION_KEY, null);
        }

        throw error;
      }
    },
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) => lastPage.nextCursor,
  });
}
