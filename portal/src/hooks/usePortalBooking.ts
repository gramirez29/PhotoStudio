import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { portalApi } from '../api/client';
import type { PortalBooking } from '../types/api/portal';

/**
 * Loads the booking a link opens. How failures are retried and when the data is refreshed come from the query client of the
 * page (see `createQueryClient`).
 * @param token Secret token taken from the URL.
 * @returns The TanStack Query result.
 */
export function usePortalBooking(token: string): UseQueryResult<PortalBooking, Error> {
  return useQuery({
    queryKey: ['portal-booking', token],
    queryFn: ({ signal }) => portalApi.getBooking(token, signal),
  });
}
