import { validateReason } from '../reasonForm';

describe('validateReason', () => {
  it('requires a reason to cancel a confirmed booking', () => {
    expect(validateReason('Cancel', 'Confirmed', '   ')).toEqual({ ok: false, error: 'Escribe el motivo.' });
  });

  it('does not require a reason to cancel a tentative booking, and sends null when it is empty', () => {
    expect(validateReason('Cancel', 'Tentative', '')).toEqual({ ok: true, reason: null });
  });

  it('always requires a reason to revert an absence', () => {
    expect(validateReason('RevertClientAbsent', 'ClientAbsent', '')).toEqual({ ok: false, error: 'Escribe el motivo.' });
  });

  it('trims the reason it accepts', () => {
    expect(validateReason('Cancel', 'Confirmed', '  Cambió de planes ')).toEqual({ ok: true, reason: 'Cambió de planes' });
    expect(validateReason('Cancel', 'Tentative', ' no vino ')).toEqual({ ok: true, reason: 'no vino' });
  });
});
