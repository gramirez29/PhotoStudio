import type { PortalBooking } from '../types/api/portal';
import type { ProofFormResult, ProofFormValues } from '../types/forms/proofForm.types';
import { depositDue, reportableAmount } from '../utils/booking';
import { formatMoney } from '../utils/format';
import { parseAmount } from '../utils/price';

/** Most characters of the reference of a transfer (the backend enforces the same limit). */
export const MAX_REFERENCE_LENGTH = 100;

/**
 * Proposes the first values of the form: the amount that moves the booking forward (the missing deposit while there is one,
 * the rest of the balance afterwards), never more than what can still be reported.
 * @param booking Booking the proof is for.
 * @returns The initial values.
 */
export function initialProofValues(booking: PortalBooking): ProofFormValues {
  const available = reportableAmount(booking);
  const due = depositDue(booking).amount;
  const suggested = Math.min(due > 0 ? due : available, available);
  return { amount: suggested > 0 ? String(Math.round(suggested)) : '', reference: '' };
}

/**
 * Validates the proof form and, when it is valid, builds the request. The same rules are enforced by the backend; checking
 * them here gives a message next to the field without a round trip.
 * @param values What the client typed.
 * @param booking Booking the proof is for; its balance and the proofs already in review bound the amount.
 * @param idempotencyKey Key of this form, created once so a double tap or a retry registers the proof once.
 * @returns The request, or the validation messages by field.
 */
export function validateProofForm(values: ProofFormValues, booking: PortalBooking, idempotencyKey: string): ProofFormResult {
  const errors: { -readonly [Field in keyof ProofFormValues]?: string } = {};

  const amount = parseAmount(values.amount);
  const available = reportableAmount(booking);
  if (amount === null) {
    errors.amount = 'Escribe el monto que transferiste.';
  } else if (amount > available) {
    errors.amount = `El monto no puede ser mayor a ${formatMoney({ amount: available, currency: booking.balance.currency })}, que es lo que falta por pagar.`;
  }

  const reference = values.reference.trim();
  if (reference.length > MAX_REFERENCE_LENGTH) {
    errors.reference = `La referencia no puede tener más de ${MAX_REFERENCE_LENGTH} caracteres.`;
  }

  if (amount === null || Object.keys(errors).length > 0) {
    return { ok: false, errors };
  }

  return { ok: true, request: { amount, ...(reference.length > 0 ? { reference } : {}), idempotencyKey } };
}
