import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { billingApi } from '../api/client';
import { useSessionStore } from '../session/sessionStore';
import type { RefundListResponse } from '../types/api/billing';

/** Prefix shared by the cache keys of the refunds list, to refresh it from the mutations. */
export const REFUNDS_QUERY_PREFIX = ['refunds'] as const;

/** How often the list is reloaded while the app is open, in milliseconds. Refunds appear when a background job runs. */
export const REFUNDS_REFETCH_MS = 60_000;

/**
 * Builds the cache key of a photographer's refunds. The photographer is part of the key so that the list of one account is
 * never shown to another that signs in on the same device.
 * @param photographerId Photographer (tenant) identifier.
 * @returns The query key.
 */
export function refundsQueryKey(photographerId: string): readonly ['refunds', string] {
  return ['refunds', photographerId] as const;
}

/**
 * Loads the refunds the signed-in photographer still has to give back and reloads them every minute.
 * @returns The TanStack Query result; the query is disabled while there is no session.
 */
export function useRefunds(): UseQueryResult<RefundListResponse, Error> {
  const photographerId = useSessionStore((state) => state.session?.photographerId ?? null);

  return useQuery({
    queryKey: refundsQueryKey(photographerId ?? ''),
    queryFn: ({ signal }) => billingApi.listRefunds(signal),
    enabled: photographerId !== null,
    refetchInterval: REFUNDS_REFETCH_MS,
  });
}
