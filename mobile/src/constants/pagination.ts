/** How many items a page of the bookings or of the notices has; the app asks the backend for exactly this many. */
export const PAGE_SIZE = 5;

/** Longest text the backend accepts to search a client by name; longer text is cut. Keep in step with `BookingListFilter.MaxNameLength`. */
export const MAX_NAME_FILTER_LENGTH = 80;

/** Milliseconds the search box waits after the last key before the list is asked again, so typing does not make a request per letter. */
export const FILTER_DEBOUNCE_MS = 400;
