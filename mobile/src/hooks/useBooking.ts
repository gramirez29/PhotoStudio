import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { bookingsApi } from '../api/client';
import type { BookingResponse } from '../api/types';

/**
 * Builds the cache key of a booking, shared by queries and future mutations that invalidate it.
 * @param bookingId Booking identifier.
 * @returns The query key.
 */
export function bookingQueryKey(bookingId: string): readonly ['booking', string] {
  return ['booking', bookingId] as const;
}

/**
 * Loads a booking from the API and keeps it cached.
 * @param bookingId Booking identifier; the query is disabled while it is empty.
 * @returns The TanStack Query result.
 */
export function useBooking(bookingId: string): UseQueryResult<BookingResponse, Error> {
  return useQuery({
    queryKey: bookingQueryKey(bookingId),
    queryFn: ({ signal }) => bookingsApi.getBooking(bookingId, signal),
    enabled: bookingId.length > 0,
  });
}
