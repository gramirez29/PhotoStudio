import { keepPreviousData, useQuery, type UseQueryResult } from '@tanstack/react-query';
import { bookingsApi } from '../api/client';
import { AGENDA_REFETCH_INTERVAL_MS, AGENDA_STALE_TIME_MS } from '../constants/agenda';
import { useSessionStore } from '../session/sessionStore';
import type { CalendarResponse } from '../types/api/calendar';
import type { DateRange, QueryRange } from '../types/utils/agenda.types';
import { toQueryRange } from '../utils/agenda/ranges';
import { bookingsQueryKey } from './useBookings';

/**
 * Builds the cache key of the bookings of a range. It lives under the key of the bookings of the photographer, on purpose: everything
 * that already refreshes the bookings (creating, editing, cancelling, rescheduling, the maintenance pass) refreshes the agenda too,
 * without the agenda being mentioned in any of them. The account is in the key so one account never sees the agenda of another that
 * signs in on the same device.
 * @param photographerId Photographer (tenant) identifier.
 * @param includeEnded Whether the bookings that are over are part of the answer.
 * @param range The range, as the backend takes it.
 * @returns The query key.
 */
export function agendaQueryKey(photographerId: string, includeEnded: boolean, range: QueryRange): readonly unknown[] {
  return [...bookingsQueryKey(photographerId), 'agenda', includeEnded, range.from, range.to];
}

/**
 * Loads the bookings of a range for the agenda and keeps them cached. The backend decides whose bookings to return from the access
 * token. While the bookings of a new range load, the ones of the previous range stay on screen, so swiping does not flash an empty
 * agenda. The range refreshes by itself every minute.
 * @param range The days to load; at most 62.
 * @param includeEnded Whether to also load the bookings that are over.
 * @returns The TanStack Query result; the query is disabled while there is no session.
 */
export function useAgendaRange(range: DateRange, includeEnded: boolean): UseQueryResult<CalendarResponse, Error> {
  const photographerId = useSessionStore((state) => state.session?.photographerId ?? null);
  const queryRange = toQueryRange(range);

  return useQuery({
    queryKey: agendaQueryKey(photographerId ?? '', includeEnded, queryRange),
    queryFn: ({ signal }) => bookingsApi.getCalendar(queryRange, includeEnded, signal),
    placeholderData: keepPreviousData,
    staleTime: AGENDA_STALE_TIME_MS,
    refetchInterval: AGENDA_REFETCH_INTERVAL_MS,
    enabled: photographerId !== null,
  });
}
