import { useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { bookingsApi } from '../api/client';
import { AGENDA_STALE_TIME_MS } from '../constants/agenda';
import { useSessionStore } from '../session/sessionStore';
import type { AgendaView } from '../types/agenda/agenda.types';
import { neighborRanges, toQueryRange } from '../utils/agenda/ranges';
import { agendaQueryKey } from './useAgendaRange';

/**
 * Loads, ahead of time, the bookings of the periods before and after the one on screen, so swiping to them shows their bookings at
 * once. A failure is ignored: the period asks for its own bookings when it is shown.
 * @param view The view that is open.
 * @param anchor A day of the period on screen.
 * @param includeEnded Whether the bookings that are over are shown.
 */
export function usePrefetchAgenda(view: AgendaView, anchor: Date, includeEnded: boolean): void {
  const queryClient = useQueryClient();
  const photographerId = useSessionStore((state) => state.session?.photographerId ?? null);
  const anchorTime = anchor.getTime();

  useEffect(() => {
    if (photographerId === null) {
      return;
    }

    for (const range of neighborRanges(view, new Date(anchorTime))) {
      const queryRange = toQueryRange(range);
      void queryClient.prefetchQuery({
        queryKey: agendaQueryKey(photographerId, includeEnded, queryRange),
        queryFn: ({ signal }) => bookingsApi.getCalendar(queryRange, includeEnded, signal),
        staleTime: AGENDA_STALE_TIME_MS,
      });
    }
  }, [queryClient, photographerId, view, anchorTime, includeEnded]);
}
