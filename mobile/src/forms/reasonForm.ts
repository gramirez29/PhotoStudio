import type { BookingStatus } from '../types/api/booking';
import type { ReasonFormResult } from '../types/forms/reasonForm.types';
import type { ReasonAction } from '../types/utils/bookingActions.types';

/**
 * Validates the reason typed for an action. Reverting an absence always needs a reason; cancelling needs one only once
 * the booking is confirmed (the backend enforces the same rule).
 * @param action Action that needs a reason.
 * @param status Current status of the booking.
 * @param text Reason as typed.
 * @returns The reason to send (null when it is optional and empty), or the message to show.
 */
export function validateReason(action: ReasonAction, status: BookingStatus, text: string): ReasonFormResult {
  const reason = text.trim();
  const required = action === 'RevertClientAbsent' || status === 'Confirmed';

  if (reason.length === 0) {
    return required ? { ok: false, error: 'Escribe el motivo.' } : { ok: true, reason: null };
  }

  return { ok: true, reason };
}
