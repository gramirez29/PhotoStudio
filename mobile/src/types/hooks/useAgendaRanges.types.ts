import type { BookingSummaryResponse } from '../api/booking';

/** What `useAgendaRanges` gives the list view: the bookings of every range together and how the loading is going. */
export interface AgendaRangesState {
  /** The bookings of all the ranges, each once, by start of the session. */
  readonly bookings: readonly BookingSummaryResponse[];
  /** Whether nothing has arrived yet: every range is still loading for the first time. */
  readonly isPending: boolean;
  /** Whether a range failed and there is nothing to show. A range that fails while others loaded is not an error of the whole. */
  readonly isError: boolean;
  /** Whether some range is loading or refreshing while there is something on screen. */
  readonly isFetching: boolean;
  /** Whether the backend left some bookings out of a range for being too many. */
  readonly truncated: boolean;
  /** Reloads every range. */
  readonly refetch: () => Promise<void>;
}
