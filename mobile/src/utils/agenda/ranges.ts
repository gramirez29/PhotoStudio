import { SCHEDULE_CHUNK_DAYS } from '../../constants/agenda';
import type { AgendaView } from '../../types/agenda/agenda.types';
import type { DateRange, QueryRange } from '../../types/utils/agenda.types';
import { addDays, addMonths, dayNumber, daysInMonth, startOfDay, startOfMonth, startOfWeek } from './dates';

/** Days a page of the three-day view covers. */
const THREE_DAYS = 3;

/** Days of a week. */
const WEEK_DAYS = 7;

/**
 * Gets the first day of the period of a view that contains a date: the day itself for the day, three-day and list views, the first
 * day of the week for the week view and the first day of the month for the month view.
 * @param view The view.
 * @param anchor A day of the period.
 * @returns The midnight that starts the period.
 */
export function periodStart(view: AgendaView, anchor: Date): Date {
  switch (view) {
    case 'week':
      return startOfWeek(anchor);
    case 'month':
      return startOfMonth(anchor);
    default:
      return startOfDay(anchor);
  }
}

/**
 * Counts the rows of weeks the grid of a month needs: the weeks it touches, from four to six.
 * @param anchor A day of the month.
 * @returns 4 to 6.
 */
export function monthWeekCount(anchor: Date): number {
  const first = startOfMonth(anchor);
  const leading = dayNumber(first) - dayNumber(startOfWeek(first));
  return Math.ceil((leading + daysInMonth(anchor)) / WEEK_DAYS);
}

/**
 * Gets the days a view shows when it is on a day: all of them, including the ones of the neighbor months that fill the grid of the
 * month view.
 * @param view The view.
 * @param anchor A day of the period.
 * @returns The range, from the midnight of its first day to the midnight after its last.
 */
export function visibleRange(view: AgendaView, anchor: Date): DateRange {
  const start = periodStart(view, anchor);
  switch (view) {
    case 'day':
      return { from: start, to: addDays(start, 1) };
    case 'threeDays':
      return { from: start, to: addDays(start, THREE_DAYS) };
    case 'week':
      return { from: start, to: addDays(start, WEEK_DAYS) };
    case 'month': {
      const first = startOfWeek(start);
      return { from: first, to: addDays(first, monthWeekCount(anchor) * WEEK_DAYS) };
    }
    default:
      return { from: start, to: addDays(start, SCHEDULE_CHUNK_DAYS) };
  }
}

/**
 * Moves the anchor by whole periods of a view: a day, three days, a week, a month or a chunk of the list.
 * @param view The view.
 * @param anchor A day of the period.
 * @param steps Periods to move; negative to go back.
 * @returns The start of the period that many steps away.
 */
export function shiftAnchor(view: AgendaView, anchor: Date, steps: number): Date {
  const start = periodStart(view, anchor);
  switch (view) {
    case 'day':
      return addDays(start, steps);
    case 'threeDays':
      return addDays(start, steps * THREE_DAYS);
    case 'week':
      return addDays(start, steps * WEEK_DAYS);
    case 'month':
      return addMonths(start, steps);
    default:
      return addDays(start, steps * SCHEDULE_CHUNK_DAYS);
  }
}

/**
 * Counts how many periods of a view there are from one anchor to another. It is the position of a page in a pager that is centered on
 * the first one, and it is the inverse of `shiftAnchor`.
 * @param view The view.
 * @param origin The anchor the pager is centered on.
 * @param target The anchor to find.
 * @returns The number of periods from `origin` to the period of `target`; negative when it is before.
 */
export function periodsBetween(view: AgendaView, origin: Date, target: Date): number {
  const from = periodStart(view, origin);
  const to = periodStart(view, target);
  const days = dayNumber(to) - dayNumber(from);
  switch (view) {
    case 'day':
      return days;
    case 'threeDays':
      return Math.floor(days / THREE_DAYS);
    case 'week':
      return Math.floor(days / WEEK_DAYS);
    case 'month':
      return (to.getFullYear() - from.getFullYear()) * 12 + (to.getMonth() - from.getMonth());
    default:
      return Math.floor(days / SCHEDULE_CHUNK_DAYS);
  }
}

/**
 * Gets the ranges of the periods before and after the one on screen, which the agenda asks for ahead of time so swiping is instant.
 * @param view The view.
 * @param anchor A day of the period on screen.
 * @returns The range of the previous period and the range of the next one.
 */
export function neighborRanges(view: AgendaView, anchor: Date): readonly [DateRange, DateRange] {
  return [visibleRange(view, shiftAnchor(view, anchor, -1)), visibleRange(view, shiftAnchor(view, anchor, 1))];
}

/**
 * Turns a range of days into what the backend takes: ISO 8601 instants.
 * @param range The range.
 * @returns The same range as UTC text.
 */
export function toQueryRange(range: DateRange): QueryRange {
  return { from: range.from.toISOString(), to: range.to.toISOString() };
}

/**
 * Tells whether an instant is inside a range.
 * @param range The range.
 * @param instant A date or instant.
 * @returns `true` when the instant is at or after the start of the range and before its end.
 */
export function rangeContains(range: DateRange, instant: Date): boolean {
  return instant.getTime() >= range.from.getTime() && instant.getTime() < range.to.getTime();
}
