import type { RescheduleFormResult, SessionSlotIso } from '../types/forms/rescheduleBookingForm.types';
import { sessionDurationMs } from '../utils/date';

/**
 * Validates the new start of a session and builds the request. Rescheduling moves the session and keeps its duration.
 * @param current Current start and end of the booking.
 * @param newStart New start, in the device time zone.
 * @param now Current instant; the new session must start after it.
 * @returns The request, or the message to show.
 */
export function validateReschedule(current: SessionSlotIso, newStart: Date, now: Date): RescheduleFormResult {
  const duration = sessionDurationMs(current.sessionStart, current.sessionEnd);
  if (duration === null) {
    return { ok: false, error: 'La reserva tiene un horario inválido y no se puede reprogramar desde la app.' };
  }

  if (newStart.getTime() <= now.getTime()) {
    return { ok: false, error: 'La sesión debe empezar en el futuro.' };
  }

  if (newStart.getTime() === new Date(current.sessionStart).getTime()) {
    return { ok: false, error: 'Elige un día u hora distintos a los actuales.' };
  }

  return {
    ok: true,
    request: {
      sessionStart: newStart.toISOString(),
      sessionEnd: new Date(newStart.getTime() + duration).toISOString(),
    },
  };
}
