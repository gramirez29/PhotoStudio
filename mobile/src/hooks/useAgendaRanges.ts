import { useQueries, useQueryClient, type UseQueryResult } from '@tanstack/react-query';
import { useCallback } from 'react';
import { bookingsApi } from '../api/client';
import { AGENDA_REFETCH_INTERVAL_MS, AGENDA_STALE_TIME_MS } from '../constants/agenda';
import { useSessionStore } from '../session/sessionStore';
import type { BookingSummaryResponse } from '../types/api/booking';
import type { CalendarResponse } from '../types/api/calendar';
import type { AgendaRangesState } from '../types/hooks/useAgendaRanges.types';
import type { DateRange } from '../types/utils/agenda.types';
import { sortByStart } from '../utils/agenda/intervals';
import { toQueryRange } from '../utils/agenda/ranges';
import { agendaQueryKey } from './useAgendaRange';
import { bookingsQueryKey } from './useBookings';

/** What the list view reads of the ranges, without the function that reloads them. */
type CombinedRanges = Omit<AgendaRangesState, 'refetch'>;

/**
 * Puts the results of the queries of every range together. It is a function of the results alone, defined outside the hook, so
 * TanStack Query can remember its answer and the screen only draws again when something it shows changed.
 * @param results The result of the query of each range.
 * @returns The bookings together and the state of the loading.
 */
function combineRanges(results: readonly UseQueryResult<CalendarResponse, Error>[]): CombinedRanges {
  const byId = new Map<string, BookingSummaryResponse>();
  for (const result of results) {
    for (const booking of result.data?.items ?? []) {
      byId.set(booking.id, booking);
    }
  }

  const bookings = sortByStart([...byId.values()]);
  return {
    bookings,
    isPending: results.length > 0 && results.every((result) => result.isPending),
    isError: bookings.length === 0 && results.some((result) => result.isError),
    isFetching: results.some((result) => result.isFetching),
    truncated: results.some((result) => result.data?.truncated === true),
  };
}

/**
 * Loads the bookings of several ranges at once, for the list view that grows as the photographer scrolls, and puts them together. Each
 * range is cached under the same key `useAgendaRange` uses, so the two share what they load, and everything that refreshes the
 * bookings of the photographer refreshes these too. A booking that crosses the limit of two ranges comes in both and appears once.
 * @param ranges The ranges to load, each at most 62 days.
 * @param includeEnded Whether to also load the bookings that are over.
 * @returns The bookings together and the state of the loading.
 */
export function useAgendaRanges(ranges: readonly DateRange[], includeEnded: boolean): AgendaRangesState {
  const queryClient = useQueryClient();
  const photographerId = useSessionStore((state) => state.session?.photographerId ?? null);

  const combined = useQueries({
    queries: ranges.map((range) => {
      const queryRange = toQueryRange(range);
      return {
        queryKey: agendaQueryKey(photographerId ?? '', includeEnded, queryRange),
        queryFn: ({ signal }: { readonly signal: AbortSignal }) => bookingsApi.getCalendar(queryRange, includeEnded, signal),
        staleTime: AGENDA_STALE_TIME_MS,
        refetchInterval: AGENDA_REFETCH_INTERVAL_MS,
        enabled: photographerId !== null,
      };
    }),
    combine: combineRanges,
  });

  const refetch = useCallback(
    () => queryClient.refetchQueries({ queryKey: [...bookingsQueryKey(photographerId ?? ''), 'agenda'], type: 'active' }),
    [queryClient, photographerId],
  );

  return { ...combined, refetch };
}
