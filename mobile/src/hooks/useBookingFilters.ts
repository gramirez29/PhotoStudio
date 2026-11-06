import { useCallback, useEffect, useMemo, useState } from 'react';
import { FILTER_DEBOUNCE_MS } from '../constants/pagination';
import type { BookingStatus } from '../types/api/booking';
import type { BookingFiltersState } from '../types/hooks/useBookingFilters.types';
import { countActiveCriteria, normalizeNameFilter } from '../utils/bookingFilters';

/**
 * Holds what the photographer chose to narrow the list of bookings: the text to search in the client name, the status, and
 * whether the panel is open. The status applies at once; the text is passed to the list only after the photographer pauses
 * typing, so the list is not asked again for every letter. Clearing applies at once, without waiting for the pause.
 * @param initialStatus The status the list starts filtered by, or `null` for any.
 * @returns The state of the filters and the functions to change it.
 */
export function useBookingFilters(initialStatus: BookingStatus | null = null): BookingFiltersState {
  const [text, setTextState] = useState('');
  const [searched, setSearched] = useState('');
  const [status, setStatus] = useState<BookingStatus | null>(initialStatus);
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => setSearched(text), FILTER_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [text]);

  const setText = useCallback((value: string) => setTextState(value), []);
  const toggleStatus = useCallback((chosen: BookingStatus) => setStatus((current) => (current === chosen ? null : chosen)), []);
  const togglePanel = useCallback(() => setOpen((current) => !current), []);
  const clear = useCallback(() => {
    setTextState('');
    setSearched('');
    setStatus(null);
  }, []);

  const filter = useMemo(() => ({ status, name: normalizeNameFilter(searched) }), [status, searched]);
  return { text, status, filter, open, activeCount: countActiveCriteria({ status, name: text }), setText, toggleStatus, togglePanel, clear };
}
