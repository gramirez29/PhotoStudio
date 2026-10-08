import { HOUR_MS, addHours, nextDayAt, sessionDurationMs, withDayOf, withTimeOf } from '../date';

describe('addHours', () => {
  it('adds whole and fractional hours without changing the original date', () => {
    const start = new Date(2026, 9, 17, 15, 30);

    expect(addHours(start, 2)).toEqual(new Date(2026, 9, 17, 17, 30));
    expect(addHours(start, 0.5)).toEqual(new Date(2026, 9, 17, 16, 0));
    expect(start).toEqual(new Date(2026, 9, 17, 15, 30));
  });
});

describe('nextDayAt', () => {
  it('returns the next calendar day at the given hour with zero minutes', () => {
    expect(nextDayAt(new Date(2026, 9, 10, 22, 45), 9)).toEqual(new Date(2026, 9, 11, 9, 0, 0, 0));
  });

  it('rolls over the end of the month', () => {
    expect(nextDayAt(new Date(2026, 9, 31, 8, 0), 9)).toEqual(new Date(2026, 10, 1, 9, 0, 0, 0));
  });
});

describe('withDayOf and withTimeOf', () => {
  it('changes the day and keeps the time of day', () => {
    const result = withDayOf(new Date(2026, 9, 17, 15, 30), new Date(2026, 10, 3, 8, 0));

    expect(result).toEqual(new Date(2026, 10, 3, 15, 30, 0, 0));
  });

  it('changes the time of day and keeps the day', () => {
    const result = withTimeOf(new Date(2026, 9, 17, 15, 30), new Date(2020, 0, 1, 9, 15));

    expect(result).toEqual(new Date(2026, 9, 17, 9, 15, 0, 0));
  });
});

describe('sessionDurationMs', () => {
  const start = new Date(2026, 9, 16, 14, 0).toISOString();
  const end = new Date(2026, 9, 16, 18, 0).toISOString();

  it('returns the length of the session', () => {
    expect(sessionDurationMs(start, end)).toBe(4 * HOUR_MS);
  });

  it('returns null when a date is invalid or the end is not after the start', () => {
    expect(sessionDurationMs('nope', end)).toBeNull();
    expect(sessionDurationMs(end, start)).toBeNull();
    expect(sessionDurationMs(start, start)).toBeNull();
  });
});
