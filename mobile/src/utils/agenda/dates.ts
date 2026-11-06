import { WEEK_STARTS_ON } from '../../constants/agenda';

/** Milliseconds in a civil day, used only to count days between dates, never to move a date. */
const MS_PER_DAY = 86_400_000;

/**
 * Counts the civil days from 1970-01-01 to the day of a date in the device time zone. Counting days instead of milliseconds keeps the
 * arithmetic right on the days the clock changes, when a day does not have 24 hours.
 * @param date A date or instant.
 * @returns The number of the day.
 */
export function dayNumber(date: Date): number {
  return Math.floor(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()) / MS_PER_DAY);
}

/**
 * Builds the midnight that starts a day, in the device time zone, from its number.
 * @param day Number of the day, as `dayNumber` gives it.
 * @returns The midnight.
 */
export function dateFromDayNumber(day: number): Date {
  const utc = new Date(day * MS_PER_DAY);
  return new Date(utc.getUTCFullYear(), utc.getUTCMonth(), utc.getUTCDate());
}

/**
 * Gets the midnight that starts the day of a date.
 * @param date A date or instant.
 * @returns The midnight, in the device time zone.
 */
export function startOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

/**
 * Moves a date by whole days.
 * @param date A date or instant.
 * @param days Days to add; negative to go back.
 * @returns The midnight of the resulting day (the time of day is dropped).
 */
export function addDays(date: Date, days: number): Date {
  return dateFromDayNumber(dayNumber(date) + days);
}

/**
 * Moves a date by whole months.
 * @param date A date or instant.
 * @param months Months to add; negative to go back.
 * @returns The first day of the resulting month, at midnight (the day of the month is dropped, so the 31st never overflows).
 */
export function addMonths(date: Date, months: number): Date {
  return new Date(date.getFullYear(), date.getMonth() + months, 1);
}

/**
 * Gets the first day of the week of a date.
 * @param date A date or instant.
 * @param weekStartsOn Day the week starts on, from 0 (Sunday) to 6 (Saturday).
 * @returns The midnight of that day.
 */
export function startOfWeek(date: Date, weekStartsOn: number = WEEK_STARTS_ON): Date {
  const behind = (date.getDay() - weekStartsOn + 7) % 7;
  return addDays(date, -behind);
}

/**
 * Gets the first day of the month of a date.
 * @param date A date or instant.
 * @returns The midnight of the first day of its month.
 */
export function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

/**
 * Counts the days of the month of a date.
 * @param date A date or instant.
 * @returns 28 to 31.
 */
export function daysInMonth(date: Date): number {
  return new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate();
}

/**
 * Tells whether two dates are on the same day of the device time zone.
 * @param a A date or instant.
 * @param b Another date or instant.
 * @returns `true` when they share the day.
 */
export function isSameDay(a: Date, b: Date): boolean {
  return dayNumber(a) === dayNumber(b);
}

/**
 * Counts the minutes that went by since the midnight of the day of a date, by the clock on the wall.
 * @param date A date or instant.
 * @returns 0 to 1439.
 */
export function minutesSinceMidnight(date: Date): number {
  return date.getHours() * 60 + date.getMinutes();
}

/**
 * Writes a day as `YYYY-MM-DD`, to use it as a key.
 * @param date A date or instant.
 * @returns The key, in the device time zone.
 */
export function toDateKey(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}
