/** Every view of the agenda, in the order the menu of the header lists them. */
export const AGENDA_VIEWS = ['schedule', 'day', 'threeDays', 'week', 'month'] as const;

/** The view the agenda opens on until the photographer chooses another: the list of days, like the calendar of the phone. */
export const DEFAULT_AGENDA_VIEW = 'schedule';

/** Day the week starts on, as `Date.getDay()` numbers it: 0 is Sunday. It is Sunday, like the calendar of the phone in Costa Rica. */
export const WEEK_STARTS_ON = 0;

/** Days of bookings the list view asks for at a time. It stays well under the 62 days the backend accepts in one request. */
export const SCHEDULE_CHUNK_DAYS = 30;

/** Minutes a booking is drawn for at least, however short it is, so it can be read and touched; it also decides when two bookings overlap on screen. */
export const MIN_EVENT_MINUTES = 30;

/** Minutes of a day, for the grid of hours. */
export const MINUTES_PER_DAY = 1440;

/** Milliseconds between two refreshes of the bookings on screen, so a booking made on another device or by the worker shows up on its own. */
export const AGENDA_REFETCH_INTERVAL_MS = 60_000;

/** Milliseconds the bookings of a range are considered fresh, so swiping back and forth does not ask for the same range again. */
export const AGENDA_STALE_TIME_MS = 30_000;
