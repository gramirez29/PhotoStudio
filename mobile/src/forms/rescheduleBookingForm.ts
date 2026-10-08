import type { RescheduleBookingRequest } from '../api/types';

/** Result of validating the reschedule form: the request to send, or the message to show. */
export type RescheduleFormResult =
  | { readonly ok: true; readonly request: RescheduleBookingRequest }
  | { readonly ok: false; readonly error: string };

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

/**
 * Validates the new start of a session and builds the request. Rescheduling moves the session and keeps its duration.
 * @param current Current start and end of the booking (ISO 8601).
 * @param current.sessionStart Current start.
 * @param current.sessionEnd Current end.
 * @param newStart New start, in the device time zone.
 * @param now Current instant; the new session must start after it.
 * @returns The request, or the message to show.
 */
export function validateReschedule(
  current: { readonly sessionStart: string; readonly sessionEnd: string },
  newStart: Date,
  now: Date,
): RescheduleFormResult {
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
