import type { BookingSummaryResponse } from '../../types/api/booking';
import type { MonthCell } from '../../types/utils/agenda.types';
import { addDays, dayNumber, isSameDay, startOfMonth, startOfWeek, toDateKey } from './dates';
import { bookingsByDay } from './intervals';
import { monthWeekCount } from './ranges';

/** Days of a week, which is how many cells a row of the grid has. */
const WEEK_DAYS = 7;

/**
 * Builds the grid of a month: one row per week it touches, each with seven days, starting on Sunday. The days of the previous and the
 * next month that fill the first and the last row are in the grid and marked as outside the month, and they carry their bookings too.
 * A booking that lasts several days is in each day it touches.
 * @param anchor Any day of the month.
 * @param bookings Bookings of the agenda; the ones outside the grid are ignored.
 * @param now The current instant, to mark today.
 * @returns The rows, from the first week to the last.
 */
export function buildMonthGrid(anchor: Date, bookings: readonly BookingSummaryResponse[], now: Date): MonthCell[][] {
  const month = startOfMonth(anchor);
  const first = startOfWeek(month);
  const weeks = monthWeekCount(anchor);
  const byDay = bookingsByDay(bookings);

  return Array.from({ length: weeks }, (_, week) =>
    Array.from({ length: WEEK_DAYS }, (_, dayOfWeek) => {
      const date = addDays(first, week * WEEK_DAYS + dayOfWeek);
      return {
        date,
        key: toDateKey(date),
        inMonth: date.getMonth() === month.getMonth() && date.getFullYear() === month.getFullYear(),
        isToday: isSameDay(date, now),
        bookings: byDay.get(dayNumber(date)) ?? [],
      };
    }),
  );
}
