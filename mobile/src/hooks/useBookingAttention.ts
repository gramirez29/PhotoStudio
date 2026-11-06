import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { bookingsApi } from '../api/client';
import { AGENDA_REFETCH_INTERVAL_MS, AGENDA_STALE_TIME_MS } from '../constants/agenda';
import { useSessionStore } from '../session/sessionStore';
import type { BookingAttentionResponse } from '../types/api/attention';
import { bookingsQueryKey } from './useBookings';

/**
 * Loads what in the bookings is waiting for the photographer: proofs of payment to verify and tentative bookings about to expire. Its
 * key lives under the one of the bookings, so everything that refreshes them (verifying a proof, cancelling, the maintenance pass)
 * refreshes these counts too. They refresh by themselves every minute.
 * @returns The TanStack Query result; the query is disabled while there is no session.
 */
export function useBookingAttention(): UseQueryResult<BookingAttentionResponse, Error> {
  const photographerId = useSessionStore((state) => state.session?.photographerId ?? null);

  return useQuery({
    queryKey: [...bookingsQueryKey(photographerId ?? ''), 'attention'],
    queryFn: ({ signal }) => bookingsApi.getAttention(signal),
    staleTime: AGENDA_STALE_TIME_MS,
    refetchInterval: AGENDA_REFETCH_INTERVAL_MS,
    enabled: photographerId !== null,
  });
}
