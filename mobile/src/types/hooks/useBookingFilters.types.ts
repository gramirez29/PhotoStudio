import type { BookingListFilter, BookingStatus } from '../api/booking';

/** What `useBookingFilters` returns: what the photographer chose, what the list should use, and how to change it. */
export interface BookingFiltersState {
  /** The text in the search box, as typed. */
  readonly text: string;
  /** The status chosen, or null for any. */
  readonly status: BookingStatus | null;
  /** The filter the list uses: the status now, and the text once the photographer stops typing for a moment. */
  readonly filter: BookingListFilter;
  /** Whether the panel of filters is open. */
  readonly open: boolean;
  /** How many criteria are being applied to the list (0 to 2), for the mark on the button. */
  readonly activeCount: number;
  /** Changes the text of the search box. */
  readonly setText: (text: string) => void;
  /** Chooses a status; choosing the one already chosen takes it off. */
  readonly toggleStatus: (status: BookingStatus) => void;
  /** Opens the panel if it is closed and closes it if it is open. */
  readonly togglePanel: () => void;
  /** Takes off every filter at once: the text and the status. */
  readonly clear: () => void;
}
