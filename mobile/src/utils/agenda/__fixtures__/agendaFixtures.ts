import type { BookingStatus, BookingSummaryResponse } from '../../../types/api/booking';

/**
 * Builds a booking of the agenda for the tests. Dates are built with the local constructor of `Date`, so what the tests say does not depend
 * on the time zone of the machine that runs them.
 * @param id Identifier of the booking.
 * @param start Start of the session.
 * @param end End of the session.
 * @param status Status of the booking.
 * @returns The booking.
 */
export function booking(id: string, start: Date, end: Date, status: BookingStatus = 'Confirmed'): BookingSummaryResponse {
  return {
    id,
    clientName: `Client ${id}`,
    packageName: 'Retrato Familiar',
    status,
    sessionStart: start.toISOString(),
    sessionEnd: end.toISOString(),
    packagePrice: { amount: 100000, currency: 'CRC' },
    balance: { amount: 50000, currency: 'CRC' },
  };
}

/**
 * Builds a booking on a day of November 2026, with the hours as the clock on the wall of the device says them.
 * @param id Identifier of the booking.
 * @param day Day of November 2026.
 * @param startHour Hour it starts, which may have a fraction (9.5 is 9:30).
 * @param endHour Hour it ends, which may have a fraction.
 * @param status Status of the booking.
 * @returns The booking.
 */
export function novemberBooking(id: string, day: number, startHour: number, endHour: number, status: BookingStatus = 'Confirmed'): BookingSummaryResponse {
  return booking(id, atHour(day, startHour), atHour(day, endHour), status);
}

/**
 * Builds an instant of November 2026 at an hour of the clock on the wall.
 * @param day Day of November; a day above 30 or below 1 spills into the next or the previous month, like `Date` does.
 * @param hour Hour, which may have a fraction (9.5 is 9:30) and may be above 24 to reach the next day.
 * @returns The instant.
 */
export function atHour(day: number, hour: number): Date {
  return new Date(2026, 10, day, Math.floor(hour), Math.round((hour % 1) * 60));
}
