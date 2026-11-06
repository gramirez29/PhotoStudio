import type { BookingSummaryResponse } from '../../types/api/booking';
import type { DateRange, DaySection } from '../../types/utils/agenda.types';
import { dateFromDayNumber, dayNumber, startOfDay, toDateKey } from './dates';
import { bookingsByDay } from './intervals';

/**
 * Groups the bookings of a range by day, for the list view. Only the days that have bookings appear, in order, and a booking that lasts
 * several days appears in each of them. Today appears even when it has none, so the list always shows where the present is.
 * @param bookings Bookings of the agenda.
 * @param range Range the list shows; days outside it are left out.
 * @param now The current instant, to find today.
 * @returns The days with their bookings, by date.
 */
export function groupByDay(bookings: readonly BookingSummaryResponse[], range: DateRange, now: Date): DaySection[] {
  const first = dayNumber(range.from);
  const last = dayNumber(new Date(range.to.getTime() - 1));
  const today = dayNumber(startOfDay(now));
  const byDay = bookingsByDay(bookings);

  const days = new Set<number>([...byDay.keys()].filter((day) => day >= first && day <= last));
  if (today >= first && today <= last) {
    days.add(today);
  }

  return [...days]
    .sort((a, b) => a - b)
    .map((day) => {
      const date = dateFromDayNumber(day);
      return { date, key: toDateKey(date), isToday: day === today, bookings: byDay.get(day) ?? [] };
    });
}
