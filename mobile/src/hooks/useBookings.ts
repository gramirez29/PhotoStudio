import { keepPreviousData, useInfiniteQuery, type InfiniteData, type UseInfiniteQueryResult } from '@tanstack/react-query';
import { bookingsApi } from '../api/client';
import { useSessionStore } from '../session/sessionStore';
import type { BookingListFilter, BookingListResponse } from '../types/api/booking';
import { NO_BOOKING_FILTER, normalizeNameFilter } from '../utils/bookingFilters';

/** Prefix shared by the cache keys of every booking list, to refresh them all at once. */
export const BOOKINGS_QUERY_PREFIX = ['bookings'] as const;

/**
 * Builds the cache key of a photographer's booking lists. The photographer is part of the key so that the list of one
 * account is never shown to another that signs in on the same device. It is the prefix of the key of every filtered list too,
 * so invalidating it refreshes the lists whatever filter they have.
 * @param photographerId Photographer (tenant) identifier.
 * @returns The query key.
 */
export function bookingsQueryKey(photographerId: string): readonly ['bookings', string] {
  return ['bookings', photographerId] as const;
}

/**
 * Loads the bookings of the signed-in photographer a page at a time, the most recently created first, and keeps the pages
 * cached. The backend decides whose bookings to return from the access token; the photographer in the key only separates the
 * cache. Each filter has its own pages in the cache; while the pages of a new filter load, the ones shown before stay on screen.
 * Refreshing the list reloads the pages already shown.
 * @param filter What narrows the list; every booking by default.
 * @returns The TanStack Query result, whose `fetchNextPage` loads the page after the last one; the query is disabled while
 * there is no session.
 */
export function useBookings(filter: BookingListFilter = NO_BOOKING_FILTER): UseInfiniteQueryResult<InfiniteData<BookingListResponse, string | null>, Error> {
  const photographerId = useSessionStore((state) => state.session?.photographerId ?? null);
  const name = normalizeNameFilter(filter.name);

  return useInfiniteQuery({
    queryKey: [...bookingsQueryKey(photographerId ?? ''), filter.status, name],
    queryFn: ({ pageParam, signal }) => bookingsApi.listBookings(pageParam, { status: filter.status, name }, signal),
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) => lastPage.nextCursor,
    placeholderData: keepPreviousData,
    enabled: photographerId !== null,
  });
}
