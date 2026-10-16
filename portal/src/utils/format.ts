import type { Money } from '../types/api/portal';

/** Locale used for every user-facing format (Spanish, Costa Rica). */
export const APP_LOCALE = 'es-CR';

/** Time zone sessions are shown in: the photographer and the client are in Costa Rica whatever the device setting says. */
export const APP_TIME_ZONE = 'America/Costa_Rica';

/**
 * Formats an amount with its currency symbol, for example "₡50 000,00".
 * @param money Amount to format.
 * @returns The formatted amount.
 */
export function formatMoney(money: Money): string {
  return new Intl.NumberFormat(APP_LOCALE, { style: 'currency', currency: money.currency }).format(money.amount);
}

/**
 * Formats a day and time, for example "sábado, 17 de octubre de 2026, 3:30 p. m.".
 * @param isoDate Instant in ISO 8601.
 * @returns The text, in the Costa Rica time zone.
 */
export function formatDateTime(isoDate: string): string {
  return new Intl.DateTimeFormat(APP_LOCALE, {
    timeZone: APP_TIME_ZONE,
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(isoDate));
}

/**
 * Formats a day without the time, for example "17 de octubre de 2026".
 * @param isoDate Instant in ISO 8601.
 * @returns The text, in the Costa Rica time zone.
 */
export function formatDay(isoDate: string): string {
  return new Intl.DateTimeFormat(APP_LOCALE, { timeZone: APP_TIME_ZONE, day: 'numeric', month: 'long', year: 'numeric' }).format(
    new Date(isoDate),
  );
}

/**
 * Formats the hours of a session, for example "3:30 p. m. a 5:30 p. m.".
 * @param startIso Session start in ISO 8601.
 * @param endIso Session end in ISO 8601.
 * @returns The text, in the Costa Rica time zone.
 */
export function formatTimeRange(startIso: string, endIso: string): string {
  const format = new Intl.DateTimeFormat(APP_LOCALE, { timeZone: APP_TIME_ZONE, hour: 'numeric', minute: '2-digit' });
  return `${format.format(new Date(startIso))} a ${format.format(new Date(endIso))}`;
}
