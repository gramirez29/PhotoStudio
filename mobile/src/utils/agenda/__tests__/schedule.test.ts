import { groupByDay } from '../schedule';
import { atHour, booking, novemberBooking } from '../__fixtures__/agendaFixtures';

/** The range of the tests: from the 1st to the 30th of November 2026, both included. */
const RANGE = { from: new Date(2026, 10, 1), to: new Date(2026, 11, 1) };

/** The current instant of the tests. */
const NOW = new Date(2026, 10, 11, 10);

describe('groupByDay', () => {
  it('lists only the days that have bookings, in order, with their bookings by start', () => {
    const sections = groupByDay(
      [novemberBooking('b', 20, 15, 16), novemberBooking('a', 20, 9, 10), novemberBooking('c', 5, 9, 10)],
      RANGE,
      new Date(2026, 9, 1),
    );

    expect(sections.map((section) => [section.key, section.bookings.map((item) => item.id)])).toEqual([
      ['2026-11-05', ['c']],
      ['2026-11-20', ['a', 'b']],
    ]);
  });

  it('always lists today, even when it has no bookings, and marks it', () => {
    const sections = groupByDay([novemberBooking('a', 20, 9, 10)], RANGE, NOW);

    expect(sections.map((section) => section.key)).toEqual(['2026-11-11', '2026-11-20']);
    expect(sections[0]).toMatchObject({ isToday: true, bookings: [] });
    expect(sections[1]?.isToday).toBe(false);
  });

  it('does not list today when it is outside the range', () => {
    expect(groupByDay([], RANGE, new Date(2026, 11, 3))).toEqual([]);
  });

  it('lists a booking that lasts several days under each of them', () => {
    const sections = groupByDay([booking('trip', atHour(11, 22), atHour(13, 2))], RANGE, new Date(2026, 9, 1));

    expect(sections.map((section) => section.key)).toEqual(['2026-11-11', '2026-11-12', '2026-11-13']);
    expect(sections.every((section) => section.bookings[0]?.id === 'trip')).toBe(true);
  });

  it('leaves out the days outside the range', () => {
    const sections = groupByDay([booking('october', new Date(2026, 9, 31, 9), new Date(2026, 9, 31, 10)), novemberBooking('inside', 3, 9, 10)], RANGE, new Date(2026, 9, 1));

    expect(sections.map((section) => section.key)).toEqual(['2026-11-03']);
  });

  it('keeps a booking that starts before the range and reaches into it, on the days that are inside', () => {
    const sections = groupByDay([booking('crosses', new Date(2026, 9, 31, 22), atHour(1, 2))], RANGE, new Date(2026, 9, 1));

    expect(sections.map((section) => section.key)).toEqual(['2026-11-01']);
  });

  it('is empty for an empty agenda away from today', () => {
    expect(groupByDay([], RANGE, new Date(2025, 0, 1))).toEqual([]);
  });
});
