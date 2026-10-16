import type { RejectPaymentFormResult } from '../types/forms/rejectPaymentForm.types';

/** Most characters of the reason of a rejection. */
export const MAX_REJECT_REASON_LENGTH = 500;

/**
 * Validates the reason typed to reject a payment proof. The reason is mandatory: the proof is never deleted, and it stays
 * on record with the reason it was rejected for.
 * @param text Reason as typed.
 * @returns The trimmed reason to send, or the message to show.
 */
export function validateRejectReason(text: string): RejectPaymentFormResult {
  const reason = text.trim();

  if (reason.length === 0) {
    return { ok: false, error: 'Escribe el motivo.' };
  }

  if (reason.length > MAX_REJECT_REASON_LENGTH) {
    return { ok: false, error: `El motivo no puede tener más de ${MAX_REJECT_REASON_LENGTH} caracteres.` };
  }

  return { ok: true, reason };
}
