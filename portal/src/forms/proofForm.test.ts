import { describe, expect, it } from 'vitest';
import { portalBooking } from '../test/fixtures';
import type { PortalPayment } from '../types/api/portal';
import { initialProofValues, MAX_REFERENCE_LENGTH, validateProofForm } from './proofForm';

const KEY = 'key-1';

/**
 * Builds a proof waiting for the photographer.
 * @param amount Amount in colones.
 * @returns The payment.
 */
function inReview(amount: number): PortalPayment {
  return { amount: { amount, currency: 'CRC' }, status: 'PendingVerification', recordedAt: '2026-10-16T10:00:00+00:00' };
}

describe('initialProofValues', () => {
  it('proposes the missing deposit while there is one', () => {
    expect(initialProofValues(portalBooking()).amount).toBe('50000');
  });

  it('proposes the rest of the balance once the deposit is covered', () => {
    const booking = portalBooking({ totalPaid: { amount: 50000, currency: 'CRC' }, balance: { amount: 50000, currency: 'CRC' } });

    expect(initialProofValues(booking).amount).toBe('50000');
  });

  it('never proposes more than what can still be reported', () => {
    const booking = portalBooking({ payments: [inReview(80000)] });

    expect(initialProofValues(booking).amount).toBe('20000');
  });

  it('proposes nothing when everything is already in review', () => {
    expect(initialProofValues(portalBooking({ payments: [inReview(100000)] })).amount).toBe('');
  });
});

describe('validateProofForm', () => {
  it('builds the request with the whole amount, the trimmed reference and the key of the form', () => {
    const result = validateProofForm({ amount: '₡50 000', reference: '  SINPE-4455 ' }, portalBooking(), KEY);

    expect(result).toEqual({ ok: true, request: { amount: 50000, reference: 'SINPE-4455', idempotencyKey: KEY } });
  });

  it('leaves the reference out when the client gave none', () => {
    const result = validateProofForm({ amount: '50000', reference: '   ' }, portalBooking(), KEY);

    expect(result).toEqual({ ok: true, request: { amount: 50000, idempotencyKey: KEY } });
  });

  it('asks for an amount', () => {
    const result = validateProofForm({ amount: 'mucho', reference: '' }, portalBooking(), KEY);

    expect(result).toEqual({ ok: false, errors: { amount: 'Escribe el monto que transferiste.' } });
  });

  it('refuses more than what is left to pay, naming the limit', () => {
    const result = validateProofForm({ amount: '100001', reference: '' }, portalBooking(), KEY);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.errors.amount).toContain('no puede ser mayor');
      expect(result.errors.amount).toContain('100');
    }
  });

  it('counts the proofs already in review against the limit', () => {
    const booking = portalBooking({ payments: [inReview(80000)] });

    expect(validateProofForm({ amount: '20000', reference: '' }, booking, KEY).ok).toBe(true);
    expect(validateProofForm({ amount: '20001', reference: '' }, booking, KEY).ok).toBe(false);
  });

  it('refuses a reference longer than the limit', () => {
    const result = validateProofForm({ amount: '1000', reference: 'x'.repeat(MAX_REFERENCE_LENGTH + 1) }, portalBooking(), KEY);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(Object.keys(result.errors)).toEqual(['reference']);
    }
  });
});
