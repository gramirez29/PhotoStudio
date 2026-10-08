/** Milliseconds in one hour. */
export const HOUR_MS = 60 * 60 * 1000;

/**
 * Adds hours to a date.
 * @param date Starting instant.
 * @param hours Hours to add; fractions are allowed.
 * @returns A new date.
 */
export function addHours(date: Date, hours: number): Date {
  return new Date(date.getTime() + hours * HOUR_MS);
}

/**
 * Builds the instant of the next calendar day at a given hour, in the device time zone.
 * @param from Reference instant.
 * @param hour Hour of the day, from 0 to 23.
 * @returns The next day at that hour, with zero minutes.
 */
export function nextDayAt(from: Date, hour: number): Date {
  return new Date(from.getFullYear(), from.getMonth(), from.getDate() + 1, hour, 0, 0, 0);
}

/**
 * Replaces the calendar day of a date, keeping its time of day.
 * @param base Date whose time of day is kept.
 * @param day Date that provides the year, month and day.
 * @returns A new date.
 */
export function withDayOf(base: Date, day: Date): Date {
  return new Date(day.getFullYear(), day.getMonth(), day.getDate(), base.getHours(), base.getMinutes(), 0, 0);
}

/**
 * Replaces the time of day of a date, keeping its calendar day.
 * @param base Date whose day is kept.
 * @param time Date that provides the hours and minutes.
 * @returns A new date.
 */
export function withTimeOf(base: Date, time: Date): Date {
  return new Date(base.getFullYear(), base.getMonth(), base.getDate(), time.getHours(), time.getMinutes(), 0, 0);
}

/**
 * Length of a session in milliseconds.
 * @param sessionStart Start (ISO 8601).
 * @param sessionEnd End (ISO 8601).
 * @returns The duration, or null when either date cannot be parsed or the end is not after the start.
 */
export function sessionDurationMs(sessionStart: string, sessionEnd: string): number | null {
  const duration = new Date(sessionEnd).getTime() - new Date(sessionStart).getTime();
  return Number.isFinite(duration) && duration > 0 ? duration : null;
}
