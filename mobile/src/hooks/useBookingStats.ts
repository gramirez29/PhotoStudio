import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { bookingsApi } from '../api/client';
import { AGENDA_REFETCH_INTERVAL_MS, AGENDA_STALE_TIME_MS } from '../constants/agenda';
import { useSessionStore } from '../session/sessionStore';
import type { BookingStatsResponse } from '../types/api/stats';
import type { QueryRange } from '../types/utils/agenda.types';
import { bookingsQueryKey } from './useBookings';

/**
 * Builds the cache key of the counts of a period. It lives under the key of the bookings of the photographer, on purpose: everything
 * that already refreshes the bookings (creating, cancelling, completing, the maintenance pass) refreshes the counts of the home
 * screen too, without the home screen being mentioned in any of them.
 * @param photographerId Photographer (tenant) identifier.
 * @param range The period, or `null` for all the bookings.
 * @returns The query key.
 */
export function statsQueryKey(photographerId: string, range: QueryRange | null): readonly unknown[] {
  return [...bookingsQueryKey(photographerId), 'stats', range?.from ?? null, range?.to ?? null];
}

/**
 * Loads the bookings of the photographer counted by status, for the chart of the home screen. The counts refresh by themselves every
 * minute, like the agenda.
 * @param range The period to count, or `null` for all the bookings.
 * @returns The TanStack Query result; the query is disabled while there is no session.
 */
export function useBookingStats(range: QueryRange | null): UseQueryResult<BookingStatsResponse, Error> {
  const photographerId = useSessionStore((state) => state.session?.photographerId ?? null);

  return useQuery({
    queryKey: statsQueryKey(photographerId ?? '', range),
    queryFn: ({ signal }) => bookingsApi.getStats(range, signal),
    staleTime: AGENDA_STALE_TIME_MS,
    refetchInterval: AGENDA_REFETCH_INTERVAL_MS,
    enabled: photographerId !== null,
  });
}
