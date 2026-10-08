import { sessionDurationMs, validateReschedule } from '../rescheduleBookingForm';

/** A fixed "now": 10 October 2026, 10:00 local time. */
const NOW = new Date(2026, 9, 10, 10, 0, 0, 0);

/** Current slot of the booking: 16 October 2026, 14:00 to 18:00 local time (four hours). */
const CURRENT = {
  sessionStart: new Date(2026, 9, 16, 14, 0).toISOString(),
  sessionEnd: new Date(2026, 9, 16, 18, 0).toISOString(),
};

describe('sessionDurationMs', () => {
  it('returns the length of the session', () => {
    expect(sessionDurationMs(CURRENT.sessionStart, CURRENT.sessionEnd)).toBe(4 * 60 * 60 * 1000);
  });

  it('returns null when a date is invalid or the end is not after the start', () => {
    expect(sessionDurationMs('nope', CURRENT.sessionEnd)).toBeNull();
    expect(sessionDurationMs(CURRENT.sessionEnd, CURRENT.sessionStart)).toBeNull();
    expect(sessionDurationMs(CURRENT.sessionStart, CURRENT.sessionStart)).toBeNull();
  });
});

describe('validateReschedule', () => {
  it('moves the session to the new start and keeps its duration', () => {
    const newStart = new Date(2026, 9, 20, 9, 30);

    const result = validateReschedule(CURRENT, newStart, NOW);

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.request).toEqual({
        sessionStart: newStart.toISOString(),
        sessionEnd: new Date(2026, 9, 20, 13, 30).toISOString(),
      });
    }
  });

  it('rejects a start that is not in the future', () => {
    const result = validateReschedule(CURRENT, NOW, NOW);

    expect(result).toEqual({ ok: false, error: 'La sesión debe empezar en el futuro.' });
  });

  it('rejects the same start the booking already has', () => {
    const result = validateReschedule(CURRENT, new Date(CURRENT.sessionStart), NOW);

    expect(result).toEqual({ ok: false, error: 'Elige un día u hora distintos a los actuales.' });
  });

  it('refuses to reschedule a booking whose current slot is invalid', () => {
    const result = validateReschedule({ sessionStart: 'x', sessionEnd: 'y' }, new Date(2026, 9, 20, 9), NOW);

    expect(result.ok).toBe(false);
  });
});
