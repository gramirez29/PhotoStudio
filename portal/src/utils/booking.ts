import type { Money, PortalBooking } from '../types/api/portal';
import { formatDay, formatMoney } from './format';

/**
 * Computes how much of the deposit is still missing. The deposit is covered by verified payments only: a proof that is still
 * in review does not count yet.
 * @param booking Booking to look at.
 * @returns The missing amount, never below zero.
 */
export function depositDue(booking: PortalBooking): Money {
  const missing = booking.depositRequired.amount - booking.totalPaid.amount;
  return { amount: Math.max(0, missing), currency: booking.depositRequired.currency };
}

/**
 * Tells whether any payment of the booking is waiting for the photographer to verify it.
 * @param booking Booking to look at.
 * @returns True when at least one proof is in review.
 */
export function hasProofInReview(booking: PortalBooking): boolean {
  return booking.payments.some((payment) => payment.status === 'PendingVerification');
}

/**
 * Writes what the client should know or do next, from the status and the money of the booking. It states facts the API already
 * decided; the page never works out by itself what a status allows.
 * @param booking Booking to look at.
 * @returns The sentences to show, in order, possibly empty.
 */
export function nextSteps(booking: PortalBooking): readonly string[] {
  const steps: string[] = [];

  switch (booking.status) {
    case 'Tentative': {
      if (booking.expiresAt !== null) {
        steps.push(`Tu fecha está apartada hasta el ${formatDay(booking.expiresAt)}.`);
      }

      const due = depositDue(booking);
      if (due.amount > 0) {
        steps.push(`Para confirmarla falta el anticipo de ${formatMoney(due)}.`);
      } else if (!booking.contractSigned) {
        steps.push('Tu anticipo está cubierto. Falta firmar el contrato con tu fotógrafo.');
      }

      break;
    }
    case 'Confirmed':
      steps.push('Tu reserva está confirmada.');
      if (booking.balance.amount > 0) {
        steps.push(`Queda un saldo de ${formatMoney(booking.balance)}.`);
      }

      break;
    case 'Completed':
      steps.push(
        booking.balance.amount > 0
          ? `Tienes un saldo pendiente de ${formatMoney(booking.balance)}. Al pagarlo recibes tus fotos finales.`
          : 'Tu sesión se realizó y estás al día con los pagos.',
      );
      break;
    case 'Cancelled':
      steps.push('Esta reserva fue cancelada. Si tienes dudas, escríbele a tu fotógrafo.');
      break;
    case 'Expired':
      steps.push('El tiempo para confirmar esta reserva venció. Si todavía la quieres, escríbele a tu fotógrafo.');
      break;
    case 'ClientAbsent':
      steps.push('La sesión no se realizó. Escríbele a tu fotógrafo para ver cómo continuar.');
      break;
  }

  if (hasProofInReview(booking)) {
    steps.push('Tu comprobante está en revisión: tu fotógrafo lo verificará pronto.');
  }

  return steps;
}
