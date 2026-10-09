import type { SettlementReason, SettlementResponse } from '../types/api/billing';
import { formatDateTime, formatMoney } from './format';
import { buildWhatsAppUrl, firstName } from './notifications';
import { PAYMENT_METHOD_LABELS } from './payments';

/** Spanish explanation of why money is kept or returned, written for the photographer. */
export const SETTLEMENT_REASON_LABELS: Readonly<Record<SettlementReason, string>> = {
  PhotographerCancelled: 'Cancelaste la sesión',
  TentativeCancelled: 'Se canceló la reserva tentativa',
  ClientCancelledInTime: 'El cliente canceló con tiempo',
  ClientCancelledLate: 'El cliente canceló tarde',
  BookingExpired: 'La reserva venció',
  ClientAbsent: 'El cliente no se presentó',
};

/**
 * Describes, line by line, what a settlement says: what was paid, what the photographer keeps and what goes back.
 * @param settlement Settlement to describe.
 * @returns The lines, in Spanish.
 */
export function describeSettlement(settlement: SettlementResponse): readonly string[] {
  const lines = [SETTLEMENT_REASON_LABELS[settlement.reason], `Pagado: ${formatMoney(settlement.totalPaid)}`];

  if (settlement.retentionStatus === 'Applied') {
    lines.push(`Te quedas con ${formatMoney(settlement.retained)} (anticipo retenido)`);
  } else if (settlement.retentionStatus === 'Reversed') {
    lines.push('La retención del anticipo se anuló');
  }

  switch (settlement.refundStatus) {
    case 'Pending':
      lines.push(`Debes devolver ${formatMoney(settlement.refundAmount)}`);
      break;
    case 'Completed':
      lines.push(describeCompletedRefund(settlement));
      break;
    case 'Voided':
      lines.push('El reembolso se anuló');
      break;
    case 'None':
      break;
  }

  return lines;
}

/**
 * Describes a refund that was given back: how much, how and when.
 * @param settlement Settlement whose refund is completed.
 * @returns The sentence, in Spanish.
 */
function describeCompletedRefund(settlement: SettlementResponse): string {
  const method = settlement.refundMethod === null ? '' : ` por ${PAYMENT_METHOD_LABELS[settlement.refundMethod]}`;
  const when = settlement.refundCompletedAt === null ? '' : ` el ${formatDateTime(settlement.refundCompletedAt)}`;
  return `Devuelto ${formatMoney(settlement.refundAmount)}${method}${when}`;
}

/**
 * Writes the WhatsApp message the photographer sends to ask the client where to transfer the refund.
 * @param settlement Settlement with a pending refund.
 * @returns The message in Spanish.
 */
export function buildRefundMessage(settlement: SettlementResponse): string {
  return (
    `Hola ${firstName(settlement.clientName)}, te escribo para devolverte ${formatMoney(settlement.refundAmount)} ` +
    `de tu reserva de ${settlement.packageName}. ¿Me confirmas el número de SINPE Móvil al que te lo envío?`
  );
}

/**
 * Builds the WhatsApp link to contact the client about a refund.
 * @param settlement Settlement with a pending refund.
 * @returns The link, or null when the client has no usable phone.
 */
export function refundWhatsAppUrl(settlement: SettlementResponse): string | null {
  return buildWhatsAppUrl(settlement.clientPhone, buildRefundMessage(settlement));
}

/**
 * Words the entry to the pending refunds on the home screen.
 * @param pendingCount How many refunds are pending.
 * @returns The label, for example "Reembolsos pendientes (2)".
 */
export function refundsLabel(pendingCount: number): string {
  return `Reembolsos pendientes (${pendingCount})`;
}
