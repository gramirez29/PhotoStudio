import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { healthApi } from '../api/client';

/** How often the readiness check is repeated while the screen is visible, in milliseconds. */
export const HEALTH_REFETCH_INTERVAL_MS = 30_000;

/**
 * Polls the backend readiness endpoint.
 * @returns The TanStack Query result; `data` is true when the API and the database are ready.
 */
export function useApiHealth(): UseQueryResult<boolean, Error> {
  return useQuery({
    queryKey: ['api-health'],
    queryFn: ({ signal }) => healthApi.isReady(signal),
    refetchInterval: HEALTH_REFETCH_INTERVAL_MS,
    retry: false,
  });
}
