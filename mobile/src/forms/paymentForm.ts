import type { BookingResponse } from '../types/api/booking';
import type { PaymentFormResult, PaymentFormValues } from '../types/forms/paymentForm.types';
import { formatMoney } from '../utils/format';
import { parsePrice } from '../utils/price';

/**
 * Validates the payment form and builds the request. The amount cannot be larger than the outstanding balance: a bigger
 * one is almost always a typing mistake, and a payment is never edited afterwards.
 * @param booking Booking the payment is recorded on.
 * @param values Values typed or picked in the form.
 * @returns The request to send, or the message to show.
 */
export function validatePayment(booking: BookingResponse, values: PaymentFormValues): PaymentFormResult {
  const amount = parsePrice(values.amount);

  if (amount === null) {
    return { ok: false, error: 'Escribe un monto mayor a cero, en colones.' };
  }

  if (amount > booking.balance.amount) {
    return { ok: false, error: `El monto supera el saldo pendiente (${formatMoney(booking.balance)}).` };
  }

  return {
    ok: true,
    request: {
      amount,
      currency: booking.balance.currency,
      method: values.method,
      idempotencyKey: values.idempotencyKey,
    },
  };
}
