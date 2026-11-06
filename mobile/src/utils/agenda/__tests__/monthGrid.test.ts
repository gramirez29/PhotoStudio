import { buildMonthGrid } from '../monthGrid';
import { atHour, booking, novemberBooking } from '../__fixtures__/agendaFixtures';

/** The current instant of the tests: the 11th of November 2026. */
const NOW = new Date(2026, 10, 11, 10);

describe('buildMonthGrid', () => {
  it('has one row per week of the month, seven days each, starting on Sunday', () => {
    const grid = buildMonthGrid(new Date(2026, 10, 20), [], NOW);

    expect(grid).toHaveLength(5);
    expect(grid.every((row) => row.length === 7)).toBe(true);
    expect(grid[0]?.[0]?.key).toBe('2026-11-01');
    expect(grid[4]?.[6]?.key).toBe('2026-12-05');
  });

  it('marks the days of the neighbor months as outside the month, and fills the grid with them', () => {
    const grid = buildMonthGrid(new Date(2026, 11, 15), [], NOW);

    expect(grid[0]?.map((cell) => [cell.key, cell.inMonth])).toEqual([
      ['2026-11-29', false],
      ['2026-11-30', false],
      ['2026-12-01', true],
      ['2026-12-02', true],
      ['2026-12-03', true],
      ['2026-12-04', true],
      ['2026-12-05', true],
    ]);
    expect(grid.at(-1)?.at(-1)?.key).toBe('2027-01-02');
    expect(grid.at(-1)?.at(-1)?.inMonth).toBe(false);
  });

  it('uses six rows for the months that need them, and four for February starting on Sunday', () => {
    expect(buildMonthGrid(new Date(2026, 7, 1), [], NOW)).toHaveLength(6);
    expect(buildMonthGrid(new Date(2026, 1, 1), [], NOW)).toHaveLength(4);
  });

  it('marks today, and only today', () => {
    const cells = buildMonthGrid(new Date(2026, 10, 1), [], NOW).flat();

    expect(cells.filter((cell) => cell.isToday).map((cell) => cell.key)).toEqual(['2026-11-11']);
  });

  it('puts each booking on its day, by start', () => {
    const grid = buildMonthGrid(
      new Date(2026, 10, 1),
      [novemberBooking('late', 11, 15, 16), novemberBooking('early', 11, 9, 10), novemberBooking('other', 20, 9, 10)],
      NOW,
    );
    const cells = new Map(grid.flat().map((cell) => [cell.key, cell]));

    expect(cells.get('2026-11-11')?.bookings.map((item) => item.id)).toEqual(['early', 'late']);
    expect(cells.get('2026-11-20')?.bookings.map((item) => item.id)).toEqual(['other']);
    expect(cells.get('2026-11-12')?.bookings).toEqual([]);
  });

  it('puts a booking that lasts several days on each of them', () => {
    const grid = buildMonthGrid(new Date(2026, 10, 1), [booking('trip', atHour(11, 22), atHour(13, 2))], NOW);
    const withBooking = grid.flat().filter((cell) => cell.bookings.length > 0).map((cell) => cell.key);

    expect(withBooking).toEqual(['2026-11-11', '2026-11-12', '2026-11-13']);
  });

  it('carries the bookings of the days of the neighbor months that are in the grid', () => {
    const grid = buildMonthGrid(new Date(2026, 11, 15), [novemberBooking('last-of-november', 30, 9, 10)], NOW);

    expect(grid[0]?.[1]).toMatchObject({ key: '2026-11-30', inMonth: false });
    expect(grid[0]?.[1]?.bookings.map((item) => item.id)).toEqual(['last-of-november']);
  });

  it('ignores the bookings outside the grid', () => {
    const grid = buildMonthGrid(new Date(2026, 10, 1), [novemberBooking('far', 11, 9, 10), booking('next-year', new Date(2027, 5, 1, 9), new Date(2027, 5, 1, 10))], NOW);

    expect(grid.flat().flatMap((cell) => cell.bookings).map((item) => item.id)).toEqual(['far']);
  });
});
