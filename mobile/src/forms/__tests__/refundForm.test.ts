import { MAX_REFUND_NOTE_LENGTH } from '../../constants/billing';
import { validateRefund } from '../refundForm';

describe('validateRefund', () => {
  it('sends only the method when there is no note', () => {
    expect(validateRefund('Cash', '   ')).toEqual({ ok: true, request: { method: 'Cash' } });
  });

  it('sends the trimmed note', () => {
    expect(validateRefund('SinpeMovil', '  ref 123  ')).toEqual({
      ok: true,
      request: { method: 'SinpeMovil', note: 'ref 123' },
    });
  });

  it('rejects a note that is too long', () => {
    expect(validateRefund('Cash', 'a'.repeat(MAX_REFUND_NOTE_LENGTH + 1))).toMatchObject({ ok: false });
    expect(validateRefund('Cash', 'a'.repeat(MAX_REFUND_NOTE_LENGTH)).ok).toBe(true);
  });
});
