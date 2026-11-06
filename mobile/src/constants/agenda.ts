/** Every view of the agenda, in the order the menu of the header lists them. */
export const AGENDA_VIEWS = ['schedule', 'day', 'threeDays', 'week', 'month'] as const;

/** The view the agenda opens on until the photographer chooses another: the week, with its hours, like the calendar of the phone. */
export const DEFAULT_AGENDA_VIEW = 'week';

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

/** The views that already have a screen. The menu of views lists the others as not available yet; a phase adds its views here. */
export const IMPLEMENTED_AGENDA_VIEWS = ['schedule', 'day', 'threeDays', 'week'] as const;

/** Chunks of the list view that can be loaded on each side of the day it opened on: 24 of 30 days is about two years each way. */
export const SCHEDULE_MAX_CHUNKS_EACH_WAY = 24;

/** Points from the edge of the list at which the next chunk of days starts loading, so it is there before the photographer arrives. */
export const SCHEDULE_LOAD_THRESHOLD = 600;

/** Chunks of 30 days the list view loads after the day it opens on, besides that day's own: three chunks, about 90 days ahead, are there when it opens. */
export const SCHEDULE_INITIAL_CHUNKS_AFTER = 2;

/** Points the height of one hour takes in the grid of hours; each half hour row is half of it. */
export const HOUR_HEIGHT = 64;

/** Points the column of hour labels takes at the left of the grid. */
export const HOUR_GUTTER_WIDTH = 52;

/** Pages of the grid the photographer can swipe to on each side of the one it opened on: for the week view it is about eight years. */
export const GRID_PAGES_EACH_WAY = 400;

/** Minutes before the current time at which the grid opens when it opens on today. */
export const GRID_OPEN_BEFORE_NOW_MINUTES = 60;

/** Minute of the day the grid opens at when it does not open on today: 7:00. */
export const GRID_DEFAULT_OPEN_MINUTES = 420;

/** Milliseconds between two moves of the line that marks the current time. */
export const NOW_LINE_INTERVAL_MS = 30_000;
