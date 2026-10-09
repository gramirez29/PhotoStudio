import { IN_PERSON_PAYMENT_METHODS } from '../constants/payments';
import type { BookingResponse, PaymentMethod } from '../types/api/booking';
import type { ContractSignatureKind } from '../types/forms/contractForm.types';

/** Spanish label of each payment method. */
export const PAYMENT_METHOD_LABELS: Readonly<Record<PaymentMethod, string>> = {
  Cash: 'Efectivo',
  SinpeMovil: 'SINPE Móvil',
  Card: 'Tarjeta',
};

/** Spanish label of each way of recording the signature of a contract. */
export const CONTRACT_SIGNATURE_LABELS: Readonly<Record<ContractSignatureKind, string>> = {
  InPerson: 'Firma en el celular',
  Paper: 'Contrato en papel',
};

/** Labels of the in-person payment methods, in the order the app lists them. */
export const IN_PERSON_PAYMENT_LABELS: readonly string[] = IN_PERSON_PAYMENT_METHODS.map(
  (method) => PAYMENT_METHOD_LABELS[method],
);

/**
 * Finds the in-person payment method that has a label.
 * @param label Label picked in the list.
 * @returns The method, or null when the label is not one of the in-person methods.
 */
export function inPersonMethodFromLabel(label: string): (typeof IN_PERSON_PAYMENT_METHODS)[number] | null {
  return IN_PERSON_PAYMENT_METHODS.find((method) => PAYMENT_METHOD_LABELS[method] === label) ?? null;
}

/**
 * Finds the signature kind that has a label.
 * @param label Label picked in the list.
 * @returns The kind, or null when the label is not one of the kinds.
 */
export function signatureKindFromLabel(label: string): ContractSignatureKind | null {
  return (Object.keys(CONTRACT_SIGNATURE_LABELS) as ContractSignatureKind[]).find(
    (kind) => CONTRACT_SIGNATURE_LABELS[kind] === label,
  ) ?? null;
}

/**
 * Suggests the amount to type: the part of the deposit that is still missing or, once the deposit is covered, the whole
 * outstanding balance. The photographer can change it.
 * @param booking Booking the payment is recorded on.
 * @returns The amount in whole colones as digits, or an empty string when nothing is owed.
 */
export function suggestedPaymentAmount(booking: BookingResponse): string {
  const missingDeposit = booking.depositRequired.amount - booking.totalPaid.amount;
  const amount = missingDeposit > 0 ? missingDeposit : booking.balance.amount;
  return amount > 0 ? String(Math.ceil(amount)) : '';
}
