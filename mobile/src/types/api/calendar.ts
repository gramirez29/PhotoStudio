import type { BookingSummaryResponse } from './booking';

/** The bookings of a range of time (backend `CalendarResponse`). */
export interface CalendarResponse {
  /** The bookings whose session overlaps the range, by start of the session. */
  readonly items: readonly BookingSummaryResponse[];
  /** Whether there were more bookings than the backend sends in one answer, and the later ones were left out. */
  readonly truncated: boolean;
}
