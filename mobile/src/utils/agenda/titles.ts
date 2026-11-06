import type { AgendaView } from '../../types/agenda/agenda.types';
import { APP_LOCALE } from '../format';
import { periodStart } from './ranges';

/**
 * Writes the name of the month of a date with its first letter in capitals, for example "Noviembre".
 * @param date A date or instant.
 * @returns The name of the month, in Spanish.
 */
export function monthName(date: Date): string {
  const name = new Intl.DateTimeFormat(APP_LOCALE, { month: 'long' }).format(date);
  return name.charAt(0).toUpperCase() + name.slice(1);
}

/**
 * Writes the title of the header of the agenda: the month of the period that is shown, and the year when it is not the current one,
 * like the header of Google Calendar. A week that crosses two months is named by the month it starts in.
 * @param view The view.
 * @param anchor A day of the period that is shown.
 * @param now The current instant, to know whether the year is the current one.
 * @returns For example "Noviembre" or "Diciembre 2027".
 */
export function agendaTitle(view: AgendaView, anchor: Date, now: Date): string {
  const start = periodStart(view, anchor);
  const month = monthName(start);
  return start.getFullYear() === now.getFullYear() ? month : `${month} ${start.getFullYear()}`;
}
