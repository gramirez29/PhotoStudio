import { MAX_REFUND_NOTE_LENGTH } from '../constants/billing';
import type { CompleteRefundRequest } from '../types/api/billing';
import type { RefundFormResult } from '../types/forms/refundForm.types';

/**
 * Validates the refund form and builds the request. The note is optional; an empty one is not sent.
 * @param method How the money was given back.
 * @param note Note as typed.
 * @returns The request to send, or the message to show.
 */
export function validateRefund(method: CompleteRefundRequest['method'], note: string): RefundFormResult {
  const trimmed = note.trim();

  if (trimmed.length > MAX_REFUND_NOTE_LENGTH) {
    return { ok: false, error: `La nota no puede pasar de ${MAX_REFUND_NOTE_LENGTH} caracteres.` };
  }

  return { ok: true, request: trimmed.length === 0 ? { method } : { method, note: trimmed } };
}
