import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { portalApi } from '../api/client';
import type { PortalBooking } from '../types/api/portal';

/**
 * Key of the cached booking of a link, so a mutation that returns the booking can replace it without asking again.
 * @param token Secret token taken from the URL.
 * @returns The query key.
 */
export function portalBookingKey(token: string): readonly [string, string] {
  return ['portal-booking', token];
}

/**
 * Loads the booking a link opens. How failures are retried and when the data is refreshed come from the query client of the
 * page (see `createQueryClient`).
 * @param token Secret token taken from the URL.
 * @returns The TanStack Query result.
 */
export function usePortalBooking(token: string): UseQueryResult<PortalBooking, Error> {
  return useQuery({
    queryKey: portalBookingKey(token),
    queryFn: ({ signal }) => portalApi.getBooking(token, signal),
  });
}
