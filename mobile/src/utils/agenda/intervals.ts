import type { BookingSummaryResponse } from '../../types/api/booking';
import { dayNumber } from './dates';

/**
 * Reads the start and the end of a booking as instants.
 * @param booking A booking of the agenda.
 * @returns The start and the end, in milliseconds since the epoch.
 */
export function bookingInterval(booking: BookingSummaryResponse): { readonly start: number; readonly end: number } {
  return { start: Date.parse(booking.sessionStart), end: Date.parse(booking.sessionEnd) };
}

/**
 * Lists the days a booking touches, in the device time zone. A session that ends exactly at midnight does not touch the next day.
 * @param booking A booking of the agenda.
 * @returns The numbers of the days, from the first to the last; empty when the dates cannot be read.
 */
export function bookingDays(booking: BookingSummaryResponse): readonly number[] {
  const { start, end } = bookingInterval(booking);
  if (Number.isNaN(start) || Number.isNaN(end) || end <= start) {
    return [];
  }

  const first = dayNumber(new Date(start));
  const last = dayNumber(new Date(end - 1));
  return Array.from({ length: last - first + 1 }, (_, index) => first + index);
}

/**
 * Orders bookings by the start of the session, and by identifier when two start together, so the order is the same on every device.
 * @param bookings Bookings to order.
 * @returns A new array, ordered.
 */
export function sortByStart(bookings: readonly BookingSummaryResponse[]): BookingSummaryResponse[] {
  return [...bookings].sort((a, b) => {
    const byStart = Date.parse(a.sessionStart) - Date.parse(b.sessionStart);
    if (byStart !== 0) {
      return byStart;
    }

    if (a.id === b.id) {
      return 0;
    }

    return a.id < b.id ? -1 : 1;
  });
}

/**
 * Groups bookings by the days they touch.
 * @param bookings Bookings of the agenda.
 * @returns The bookings of each day number, by start; a booking that lasts several days is in each of them.
 */
export function bookingsByDay(bookings: readonly BookingSummaryResponse[]): Map<number, BookingSummaryResponse[]> {
  const byDay = new Map<number, BookingSummaryResponse[]>();
  for (const booking of sortByStart(bookings)) {
    for (const day of bookingDays(booking)) {
      const list = byDay.get(day);
      if (list === undefined) {
        byDay.set(day, [booking]);
      } else {
        list.push(booking);
      }
    }
  }

  return byDay;
}
