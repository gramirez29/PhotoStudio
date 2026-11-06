import type { AgendaView } from '../../types/agenda/agenda.types';
import type { BookingSummaryResponse } from '../../types/api/booking';
import { APP_LOCALE, formatTime } from '../format';
import { BOOKING_STATUS_LABELS } from '../labels';

/** The name of each view, as the menu of views writes it. */
export const AGENDA_VIEW_LABELS: Readonly<Record<AgendaView, string>> = {
  schedule: 'Agenda',
  day: 'Día',
  threeDays: '3 días',
  week: 'Semana',
  month: 'Mes',
};

/**
 * Writes the weekday of a date in three letters, for example "mié".
 * @param date A date or instant.
 * @returns The short name of the weekday, in Spanish and in lower case.
 */
export function weekdayShort(date: Date): string {
  return new Intl.DateTimeFormat(APP_LOCALE, { weekday: 'short' }).format(date).replace('.', '');
}

/**
 * Writes the hours of a booking, for example "3:00 p. m. – 5:00 p. m.", in the time of the phone.
 * @param booking A booking of the agenda.
 * @returns The start and the end of the session.
 */
export function formatBookingHours(booking: BookingSummaryResponse): string {
  return `${formatTime(new Date(booking.sessionStart))} – ${formatTime(new Date(booking.sessionEnd))}`;
}

/**
 * Writes what a screen reader says about a booking of the agenda: who, what, when and in what state.
 * @param booking A booking of the agenda.
 * @returns For example "Reserva de María Pérez, Retrato Familiar, de 3:00 p. m. a 5:00 p. m., confirmada".
 */
export function describeBookingForReader(booking: BookingSummaryResponse): string {
  const hours = `de ${formatTime(new Date(booking.sessionStart))} a ${formatTime(new Date(booking.sessionEnd))}`;
  return `Reserva de ${booking.clientName}, ${booking.packageName}, ${hours}, ${BOOKING_STATUS_LABELS[booking.status].toLowerCase()}`;
}
