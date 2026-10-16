import { MAX_REJECT_REASON_LENGTH, validateRejectReason } from '../rejectPaymentForm';

describe('validateRejectReason', () => {
  it('returns the trimmed reason', () => {
    expect(validateRejectReason('  No aparece en el banco ')).toEqual({ ok: true, reason: 'No aparece en el banco' });
  });

  it('requires a reason', () => {
    expect(validateRejectReason('   ')).toEqual({ ok: false, error: 'Escribe el motivo.' });
  });

  it('rejects a reason longer than the limit', () => {
    const result = validateRejectReason('a'.repeat(MAX_REJECT_REASON_LENGTH + 1));

    expect(result.ok).toBe(false);
  });
});
