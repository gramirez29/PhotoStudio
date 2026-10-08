import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { bookingsApi } from '../api/client';
import { useSessionStore } from '../session/sessionStore';
import type { BookingSummaryResponse } from '../types/api/booking';

/** Prefix shared by the cache keys of every booking list, to refresh them all at once. */
export const BOOKINGS_QUERY_PREFIX = ['bookings'] as const;

/**
 * Builds the cache key of a photographer's booking list. The photographer is part of the key so that the list of one
 * account is never shown to another that signs in on the same device.
 * @param photographerId Photographer (tenant) identifier.
 * @returns The query key.
 */
export function bookingsQueryKey(photographerId: string): readonly ['bookings', string] {
  return ['bookings', photographerId] as const;
}

/**
 * Loads the bookings of the signed-in photographer and keeps them cached. The backend decides whose bookings to return
 * from the access token; the photographer in the key only separates the cache.
 * @returns The TanStack Query result; the query is disabled while there is no session.
 */
export function useBookings(): UseQueryResult<readonly BookingSummaryResponse[], Error> {
  const photographerId = useSessionStore((state) => state.session?.photographerId ?? null);

  return useQuery({
    queryKey: bookingsQueryKey(photographerId ?? ''),
    queryFn: ({ signal }) => bookingsApi.listBookings(signal),
    enabled: photographerId !== null,
  });
}
