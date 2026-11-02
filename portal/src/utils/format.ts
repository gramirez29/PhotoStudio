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
 * Formats the size of a file in megabytes with one decimal and a point, for example "4.2 MB". It is the size as the cards of the
 * photos show it.
 * @param bytes Size in bytes.
 * @returns The text.
 */
export function formatMegabytes(bytes: number): string {
  return `${(bytes / 1_048_576).toFixed(1)} MB`;
}

/**
 * Formats a date as day, month and year with two digits each, in the Costa Rica time zone, for example "17/10/2026".
 * @param isoDate Instant in ISO 8601.
 * @returns The text.
 */
export function formatShortDate(isoDate: string): string {
  return new Intl.DateTimeFormat(APP_LOCALE, { timeZone: APP_TIME_ZONE, day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(isoDate));
}

/**
 * Formats the size of a file for people, in megabytes from one on and in kilobytes below, for example "4,2 MB" or "850 KB".
 * @param bytes Size in bytes.
 * @returns The text.
 */
export function formatBytes(bytes: number): string {
  const megabytes = bytes / 1_048_576;
  if (megabytes >= 1) {
    return `${megabytes.toLocaleString(APP_LOCALE, { maximumFractionDigits: 1 })} MB`;
  }

  return `${Math.max(1, Math.round(bytes / 1024)).toLocaleString(APP_LOCALE)} KB`;
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
