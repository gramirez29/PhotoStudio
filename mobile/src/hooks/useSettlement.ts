import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { billingApi } from '../api/client';
import { ApiError } from '../api/httpClient';
import type { SettlementResponse } from '../types/api/billing';

/** Prefix shared by the cache keys of the settlements, to refresh them all at once. */
export const SETTLEMENT_QUERY_PREFIX = ['settlement'] as const;

/**
 * Builds the cache key of the settlement of a booking.
 * @param bookingId Booking identifier.
 * @returns The query key.
 */
export function settlementQueryKey(bookingId: string): readonly ['settlement', string] {
  return ['settlement', bookingId] as const;
}

/**
 * Loads the settlement of a booking. A booking that ended with nothing paid has none: the backend answers 404, which here
 * means "nothing to settle" and is returned as null instead of as a failure.
 * @param bookingId Booking identifier.
 * @param enabled Whether to ask at all; only bookings that ended can have a settlement.
 * @returns The TanStack Query result; its data is null when there is no settlement.
 */
export function useSettlement(bookingId: string, enabled: boolean): UseQueryResult<SettlementResponse | null, Error> {
  return useQuery({
    queryKey: settlementQueryKey(bookingId),
    queryFn: async ({ signal }) => {
      try {
        return await billingApi.getSettlement(bookingId, signal);
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) {
          return null;
        }

        throw error;
      }
    },
    enabled: enabled && bookingId.length > 0,
  });
}
