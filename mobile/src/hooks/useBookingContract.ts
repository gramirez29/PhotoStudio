import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { bookingsApi } from '../api/client';
import type { BookingContractResponse } from '../types/api/booking';

/**
 * Key of the cached contract of a booking.
 * @param bookingId Booking identifier.
 * @returns The query key.
 */
export function bookingContractQueryKey(bookingId: string): readonly ['booking-contract', string] {
  return ['booking-contract', bookingId] as const;
}

/**
 * Loads the contract of a booking, with its data filled in, so the client can read it on the phone before signing. It is read
 * again every time the screen opens: the version the client signs must be the one the server holds now.
 * @param bookingId Booking identifier.
 * @param enabled Whether to load it; false while there is nothing to read, for example for a contract on paper.
 * @returns The TanStack Query result.
 */
export function useBookingContract(bookingId: string, enabled: boolean): UseQueryResult<BookingContractResponse, Error> {
  return useQuery({
    queryKey: bookingContractQueryKey(bookingId),
    queryFn: ({ signal }) => bookingsApi.getContract(bookingId, signal),
    enabled: enabled && bookingId.length > 0,
    staleTime: 0,
    gcTime: 0,
  });
}
