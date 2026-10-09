import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import type { PaymentFormValues } from '../../types/forms/paymentForm.types';
import { validatePayment } from '../paymentForm';

const booking = parseBooking(bookingPayload);

/**
 * Builds the values of the form, valid unless overridden.
 * @param overrides Values to replace.
 * @returns The values.
 */
function values(overrides: Partial<PaymentFormValues> = {}): PaymentFormValues {
  return { amount: '10000', method: 'Cash', idempotencyKey: 'pay-key', ...overrides };
}

describe('validatePayment', () => {
  it('builds the request with the currency of the booking and the idempotency key', () => {
    expect(validatePayment(booking, values({ amount: '10 000', method: 'SinpeMovil' }))).toEqual({
      ok: true,
      request: { amount: 10000, currency: booking.balance.currency, method: 'SinpeMovil', idempotencyKey: 'pay-key' },
    });
  });

  it('rejects an empty, zero or non-numeric amount', () => {
    for (const amount of ['', '0', 'abc', '-5']) {
      expect(validatePayment(booking, values({ amount }))).toEqual({
        ok: false,
        error: 'Escribe un monto mayor a cero, en colones.',
      });
    }
  });

  it('rejects an amount larger than the outstanding balance', () => {
    const result = validatePayment(booking, values({ amount: String(booking.balance.amount + 1) }));

    expect(result).toMatchObject({ ok: false });
    expect(result.ok ? '' : result.error).toContain('supera el saldo pendiente');
  });

  it('accepts paying exactly the whole balance', () => {
    expect(validatePayment(booking, values({ amount: String(booking.balance.amount) })).ok).toBe(true);
  });
});
