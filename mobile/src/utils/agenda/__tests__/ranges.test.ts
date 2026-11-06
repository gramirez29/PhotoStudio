import { AGENDA_VIEWS, SCHEDULE_CHUNK_DAYS } from '../../../constants/agenda';
import { dayNumber } from '../dates';
import {
  monthWeekCount,
  neighborRanges,
  periodStart,
  periodsBetween,
  rangeContains,
  shiftAnchor,
  toQueryRange,
  visibleRange,
} from '../ranges';

/** A Wednesday of November 2026, in the middle of the afternoon. */
const WEDNESDAY = new Date(2026, 10, 11, 15, 30);

describe('periodStart', () => {
  it('is the day itself for the day, three-day and list views', () => {
    expect(periodStart('day', WEDNESDAY)).toEqual(new Date(2026, 10, 11));
    expect(periodStart('threeDays', WEDNESDAY)).toEqual(new Date(2026, 10, 11));
    expect(periodStart('schedule', WEDNESDAY)).toEqual(new Date(2026, 10, 11));
  });

  it('is the Sunday for the week and the first of the month for the month', () => {
    expect(periodStart('week', WEDNESDAY)).toEqual(new Date(2026, 10, 8));
    expect(periodStart('month', WEDNESDAY)).toEqual(new Date(2026, 10, 1));
  });
});

describe('monthWeekCount', () => {
  it.each([
    [new Date(2026, 1, 10), 4], // February 2026 starts on a Sunday and has 28 days.
    [new Date(2026, 10, 10), 5], // November 2026 starts on a Sunday and has 30 days.
    [new Date(2026, 11, 10), 5], // December 2026 starts on a Tuesday.
    [new Date(2026, 7, 10), 6], // August 2026 starts on a Saturday and has 31 days.
  ])('is the number of weeks the month touches (%s)', (anchor, weeks) => {
    expect(monthWeekCount(anchor)).toBe(weeks);
  });
});

describe('visibleRange', () => {
  it('covers one day, three days and a week', () => {
    expect(visibleRange('day', WEDNESDAY)).toEqual({ from: new Date(2026, 10, 11), to: new Date(2026, 10, 12) });
    expect(visibleRange('threeDays', WEDNESDAY)).toEqual({ from: new Date(2026, 10, 11), to: new Date(2026, 10, 14) });
    expect(visibleRange('week', WEDNESDAY)).toEqual({ from: new Date(2026, 10, 8), to: new Date(2026, 10, 15) });
  });

  it('covers the whole grid of the month, with the days of the neighbor months that fill it', () => {
    expect(visibleRange('month', WEDNESDAY)).toEqual({ from: new Date(2026, 10, 1), to: new Date(2026, 11, 6) });
    expect(visibleRange('month', new Date(2026, 11, 15))).toEqual({ from: new Date(2026, 10, 29), to: new Date(2027, 0, 3) });
  });

  it('covers a chunk of days for the list', () => {
    const range = visibleRange('schedule', WEDNESDAY);

    expect(range.from).toEqual(new Date(2026, 10, 11));
    expect(dayNumber(range.to) - dayNumber(range.from)).toBe(SCHEDULE_CHUNK_DAYS);
  });

  it.each(AGENDA_VIEWS)('never asks for more than the 62 days the backend accepts (%s)', (view) => {
    for (let month = 0; month < 12; month += 1) {
      const range = visibleRange(view, new Date(2026, month, 15));

      expect(dayNumber(range.to) - dayNumber(range.from)).toBeLessThanOrEqual(62);
      expect(range.to.getTime()).toBeGreaterThan(range.from.getTime());
    }
  });
});

describe('shiftAnchor', () => {
  it('moves by a day, three days, a week, a month and a chunk of the list', () => {
    expect(shiftAnchor('day', WEDNESDAY, 1)).toEqual(new Date(2026, 10, 12));
    expect(shiftAnchor('threeDays', WEDNESDAY, 1)).toEqual(new Date(2026, 10, 14));
    expect(shiftAnchor('week', WEDNESDAY, 1)).toEqual(new Date(2026, 10, 15));
    expect(shiftAnchor('month', WEDNESDAY, 1)).toEqual(new Date(2026, 11, 1));
    expect(dayNumber(shiftAnchor('schedule', WEDNESDAY, 1)) - dayNumber(WEDNESDAY)).toBe(SCHEDULE_CHUNK_DAYS);
  });

  it('goes back with a negative number and stays where it is with zero', () => {
    expect(shiftAnchor('week', WEDNESDAY, -1)).toEqual(new Date(2026, 10, 1));
    expect(shiftAnchor('month', WEDNESDAY, -1)).toEqual(new Date(2026, 9, 1));
    expect(shiftAnchor('day', WEDNESDAY, 0)).toEqual(new Date(2026, 10, 11));
  });
});

describe('periodsBetween', () => {
  it.each(AGENDA_VIEWS)('is the inverse of shiftAnchor (%s)', (view) => {
    for (const steps of [-30, -7, -1, 0, 1, 2, 9, 40]) {
      expect(periodsBetween(view, WEDNESDAY, shiftAnchor(view, WEDNESDAY, steps))).toBe(steps);
    }
  });

  it('is zero for any day of the same period', () => {
    expect(periodsBetween('week', new Date(2026, 10, 8), new Date(2026, 10, 14))).toBe(0);
    expect(periodsBetween('month', new Date(2026, 10, 1), new Date(2026, 10, 30))).toBe(0);
  });

  it('counts the months across a year', () => {
    expect(periodsBetween('month', new Date(2026, 10, 15), new Date(2027, 1, 3))).toBe(3);
    expect(periodsBetween('month', new Date(2026, 10, 15), new Date(2025, 10, 3))).toBe(-12);
  });
});

describe('neighborRanges', () => {
  it('are the ranges of the previous and the next period', () => {
    const [previous, next] = neighborRanges('week', WEDNESDAY);

    expect(previous).toEqual({ from: new Date(2026, 10, 1), to: new Date(2026, 10, 8) });
    expect(next).toEqual({ from: new Date(2026, 10, 15), to: new Date(2026, 10, 22) });
  });
});

describe('toQueryRange', () => {
  it('writes the range as ISO 8601 instants', () => {
    const range = { from: new Date(2026, 10, 8), to: new Date(2026, 10, 15) };

    expect(toQueryRange(range)).toEqual({ from: range.from.toISOString(), to: range.to.toISOString() });
  });
});

describe('rangeContains', () => {
  it('includes the start and leaves out the end', () => {
    const range = { from: new Date(2026, 10, 8), to: new Date(2026, 10, 15) };

    expect(rangeContains(range, new Date(2026, 10, 8))).toBe(true);
    expect(rangeContains(range, new Date(2026, 10, 14, 23, 59))).toBe(true);
    expect(rangeContains(range, new Date(2026, 10, 15))).toBe(false);
    expect(rangeContains(range, new Date(2026, 10, 7, 23, 59))).toBe(false);
  });
});
