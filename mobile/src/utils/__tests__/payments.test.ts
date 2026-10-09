import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { createIdempotencyKey } from '../idempotency';
import {
  IN_PERSON_PAYMENT_LABELS,
  inPersonMethodFromLabel,
  signatureKindFromLabel,
  suggestedPaymentAmount,
} from '../payments';

const booking = parseBooking(bookingPayload);

describe('in-person payment labels', () => {
  it('offers cash and SINPE Móvil, and maps a label back to its method', () => {
    expect(IN_PERSON_PAYMENT_LABELS).toEqual(['Efectivo', 'SINPE Móvil']);
    expect(inPersonMethodFromLabel('Efectivo')).toBe('Cash');
    expect(inPersonMethodFromLabel('SINPE Móvil')).toBe('SinpeMovil');
    expect(inPersonMethodFromLabel('Tarjeta')).toBeNull();
  });

  it('maps a signature label back to its kind', () => {
    expect(signatureKindFromLabel('Firma en el celular')).toBe('InPerson');
    expect(signatureKindFromLabel('Contrato en papel')).toBe('Paper');
    expect(signatureKindFromLabel('Otra')).toBeNull();
  });
});

describe('suggestedPaymentAmount', () => {
  it('suggests the part of the deposit that is still missing', () => {
    const missing = {
      ...booking,
      depositRequired: { amount: 50000, currency: 'CRC' },
      totalPaid: { amount: 20000, currency: 'CRC' },
      balance: { amount: 80000, currency: 'CRC' },
    };

    expect(suggestedPaymentAmount(missing)).toBe('30000');
  });

  it('suggests the whole balance once the deposit is covered', () => {
    const covered = {
      ...booking,
      depositRequired: { amount: 50000, currency: 'CRC' },
      totalPaid: { amount: 50000, currency: 'CRC' },
      balance: { amount: 50000, currency: 'CRC' },
    };

    expect(suggestedPaymentAmount(covered)).toBe('50000');
  });

  it('suggests nothing when nothing is owed', () => {
    const paid = { ...booking, totalPaid: booking.packagePrice, balance: { amount: 0, currency: 'CRC' } };

    expect(suggestedPaymentAmount({ ...paid, depositRequired: { amount: 0, currency: 'CRC' } })).toBe('');
  });
});

describe('createIdempotencyKey', () => {
  it('builds a key from the time and the random source', () => {
    expect(createIdempotencyKey(() => 0, 36)).toBe('pay-10-0000000000000000');
    expect(createIdempotencyKey(() => 0.999, 0)).toBe('pay-0-ffffffffffffffff');
  });

  it('gives different keys on different calls', () => {
    expect(createIdempotencyKey()).not.toBe(createIdempotencyKey());
  });
});
