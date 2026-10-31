import { useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';
import { ApiError } from '../api/httpClient';
import { STUDIO_SESSION_KEY } from './useStudioSession';

/**
 * Gives a function that runs a request of the web area and, if the server answers that the session is over (a 401), clears the
 * cached session, which shows the sign-in instead of a page that cannot load. The error still goes on to whoever asked.
 * @returns The function that guards a request.
 */
export function useSessionGuard(): <T>(request: () => Promise<T>) => Promise<T> {
  const queryClient = useQueryClient();

  return useCallback(
    async <T>(request: () => Promise<T>): Promise<T> => {
      try {
        return await request();
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          queryClient.setQueryData(STUDIO_SESSION_KEY, null);
        }

        throw error;
      }
    },
    [queryClient],
  );
}
