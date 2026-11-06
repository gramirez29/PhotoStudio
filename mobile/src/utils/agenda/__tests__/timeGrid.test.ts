import { HOUR_HEIGHT } from '../../../constants/agenda';
import type { PositionedEvent } from '../../../types/utils/agenda.types';
import { novemberBooking } from '../__fixtures__/agendaFixtures';
import { eventFrame, gridDayCount, gridPageDays, hourLabel, initialGridOffset, minutesToOffset } from '../timeGrid';

/**
 * Builds a placed event for the tests.
 * @param overrides Parts to replace.
 * @returns The event: 9:00 to 10:00, alone in its column.
 */
function placed(overrides: Partial<PositionedEvent> = {}): PositionedEvent {
  return {
    booking: novemberBooking('a', 11, 9, 10),
    startMinutes: 540,
    endMinutes: 600,
    visualEndMinutes: 600,
    column: 0,
    columnSpan: 1,
    columns: 1,
    continuesBefore: false,
    continuesAfter: false,
    ...overrides,
  };
}

describe('timeGrid', () => {
  it('knows how many days each view shows', () => {
    expect(gridDayCount('day')).toBe(1);
    expect(gridDayCount('threeDays')).toBe(3);
    expect(gridDayCount('week')).toBe(7);
  });

  it('gives the days of a page, with the week starting on Sunday', () => {
    const origin = new Date(2026, 10, 11);

    expect(gridPageDays('week', origin, 0).map((day) => day.getDate())).toEqual([8, 9, 10, 11, 12, 13, 14]);
    expect(gridPageDays('week', origin, -1)[0]?.getDate()).toBe(1);
    expect(gridPageDays('threeDays', origin, 1).map((day) => day.getDate())).toEqual([14, 15, 16]);
    expect(gridPageDays('day', origin, 2).map((day) => day.getDate())).toEqual([13]);
  });

  it('writes the labels of the hours, with none for midnight', () => {
    expect(hourLabel(0)).toBe('');
    expect(hourLabel(1)).toBe('1 a. m.');
    expect(hourLabel(12)).toBe('12 p. m.');
    expect(hourLabel(15)).toBe('3 p. m.');
  });

  it('turns minutes into points', () => {
    expect(minutesToOffset(60)).toBe(HOUR_HEIGHT);
    expect(minutesToOffset(30)).toBe(HOUR_HEIGHT / 2);
  });

  it('draws a block as tall as the session lasts', () => {
    const frame = eventFrame(placed(), 100);

    expect(frame.top).toBe(9 * HOUR_HEIGHT);
    expect(frame.height).toBe(HOUR_HEIGHT);
    expect(frame.left).toBe(0);
    expect(frame.width).toBe(98);
  });

  it('shares the width of the column between the blocks that overlap and lets one stretch over free columns', () => {
    const second = eventFrame(placed({ column: 1, columns: 2 }), 100);
    const stretched = eventFrame(placed({ column: 0, columns: 2, columnSpan: 2 }), 100);

    expect(second.left).toBe(50);
    expect(second.width).toBe(48);
    expect(stretched.width).toBe(98);
  });

  it('gives a short session the height of the minimum the layout gave it', () => {
    const frame = eventFrame(placed({ endMinutes: 550, visualEndMinutes: 570 }), 100);

    expect(frame.height).toBe(HOUR_HEIGHT / 2);
  });

  it('opens an hour before now on today and at 7:00 on any other day, never above the top', () => {
    expect(initialGridOffset(true, new Date(2026, 10, 11, 15, 0))).toBe(14 * HOUR_HEIGHT);
    expect(initialGridOffset(true, new Date(2026, 10, 11, 0, 20))).toBe(0);
    expect(initialGridOffset(false, new Date(2026, 10, 11, 15, 0))).toBe(7 * HOUR_HEIGHT);
  });
});
