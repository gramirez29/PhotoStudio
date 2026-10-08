import {
  ACTION_VARIANTS,
  confirmationFor,
  parseReasonAction,
  reasonFormCopy,
  splitActions,
} from '../bookingActions';

describe('splitActions', () => {
  it('keeps only the allowed actions the app supports, in the order the app lists them', () => {
    const result = splitActions(['Cancel', 'Complete', 'Reschedule']);

    expect(result.supported).toEqual(['Reschedule', 'Complete', 'Cancel']);
    expect(result.pending).toEqual([]);
  });

  it('puts the allowed actions the app cannot perform yet apart', () => {
    const result = splitActions(['RecordInPersonPayment', 'SignContract', 'Cancel']);

    expect(result.supported).toEqual(['Cancel']);
    expect(result.pending).toEqual(['RecordInPersonPayment', 'SignContract']);
  });

  it('returns nothing for a booking without allowed actions', () => {
    expect(splitActions([])).toEqual({ supported: [], pending: [] });
  });
});

describe('ACTION_VARIANTS', () => {
  it('marks the actions that free the slot or end the booking as dangerous', () => {
    expect(ACTION_VARIANTS.Cancel).toBe('danger');
    expect(ACTION_VARIANTS.MarkClientAbsent).toBe('danger');
    expect(ACTION_VARIANTS.Reschedule).toBe('primary');
    expect(ACTION_VARIANTS.Complete).toBe('primary');
    expect(ACTION_VARIANTS.RevertClientAbsent).toBe('primary');
  });
});

describe('confirmationFor', () => {
  it('names the client in the completion question', () => {
    const texts = confirmationFor('Complete', 'María Pérez');

    expect(texts.message).toContain('María Pérez');
    expect(texts.confirmLabel).toBe('Completar');
  });

  it('warns that marking the client absent frees the slot', () => {
    const texts = confirmationFor('MarkClientAbsent', 'Carlos Rojas');

    expect(texts.message).toContain('Carlos Rojas');
    expect(texts.message).toContain('libera el horario');
  });
});

describe('parseReasonAction', () => {
  it('accepts the actions that need a reason', () => {
    expect(parseReasonAction('Cancel')).toBe('Cancel');
    expect(parseReasonAction('RevertClientAbsent')).toBe('RevertClientAbsent');
  });

  it('uses the first value when the parameter comes as a list', () => {
    expect(parseReasonAction(['Cancel', 'Complete'])).toBe('Cancel');
  });

  it('rejects anything else', () => {
    expect(parseReasonAction('Complete')).toBeNull();
    expect(parseReasonAction('cancel')).toBeNull();
    expect(parseReasonAction(undefined)).toBeNull();
    expect(parseReasonAction([])).toBeNull();
  });
});

describe('reasonFormCopy', () => {
  it('has its own texts for each action', () => {
    expect(reasonFormCopy('Cancel').submitLabel).toBe('Cancelar reserva');
    expect(reasonFormCopy('RevertClientAbsent').submitLabel).toBe('Revertir ausencia');
  });
});
