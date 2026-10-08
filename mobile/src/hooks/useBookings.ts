import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { bookingsApi } from '../api/client';
import type { BookingSummaryResponse } from '../api/types';

/**
 * Builds the cache key of a photographer's booking list, shared by queries and future mutations that invalidate it.
 * @param photographerId Photographer (tenant) identifier.
 * @returns The query key.
 */
export function bookingsQueryKey(photographerId: string): readonly ['bookings', string] {
  return ['bookings', photographerId] as const;
}

/**
 * Loads the photographer's bookings and keeps them cached.
 * @param photographerId Photographer identifier; the query is disabled while it is null.
 * @returns The TanStack Query result.
 */
export function useBookings(photographerId: string | null): UseQueryResult<readonly BookingSummaryResponse[], Error> {
  return useQuery({
    queryKey: bookingsQueryKey(photographerId ?? ''),
    queryFn: ({ signal }) => bookingsApi.listBookings(photographerId ?? '', signal),
    enabled: photographerId !== null,
  });
}
