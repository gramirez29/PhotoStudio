import type { MoneyResponse } from '../api/types';

/** Locale used for every user-facing format (Spanish, Costa Rica). */
export const APP_LOCALE = 'es-CR';

/**
 * Formats an amount with its currency symbol, for example "₡50 000,00".
 * @param money Amount to format.
 * @returns The formatted amount.
 */
export function formatMoney(money: MoneyResponse): string {
  return new Intl.NumberFormat(APP_LOCALE, { style: 'currency', currency: money.currency }).format(money.amount);
}

/**
 * Formats an ISO 8601 instant as a medium date and short time in the device time zone.
 * @param isoDate Instant in ISO 8601 format.
 * @returns The formatted date, or the original text when it cannot be parsed.
 */
export function formatDateTime(isoDate: string): string {
  const date = new Date(isoDate);
  if (Number.isNaN(date.getTime())) {
    return isoDate;
  }

  return new Intl.DateTimeFormat(APP_LOCALE, { dateStyle: 'medium', timeStyle: 'short' }).format(date);
}

/**
 * Formats a date as a long weekday and date in the device time zone, for example "sábado, 17 de octubre de 2026".
 * @param date Date to format.
 * @returns The formatted date.
 */
export function formatDate(date: Date): string {
  return new Intl.DateTimeFormat(APP_LOCALE, { dateStyle: 'full' }).format(date);
}

/**
 * Formats the time of day in the device time zone.
 * @param date Date to format.
 * @returns The formatted time, for example "9:00" or "2:30 p. m." depending on the device.
 */
export function formatTime(date: Date): string {
  return new Intl.DateTimeFormat(APP_LOCALE, { timeStyle: 'short' }).format(date);
}
