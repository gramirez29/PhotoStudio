import { formatTime } from '../../format';
import { AGENDA_VIEW_LABELS, describeBookingForReader, formatBookingHours, weekdayShort } from '../labels';
import { groupByDay } from '../schedule';
import { buildScheduleRows, pageToLoad } from '../scheduleRows';
import { effectiveAgendaView, isViewImplemented } from '../views';
import { atHour, novemberBooking } from '../__fixtures__/agendaFixtures';

const NOW = new Date(2026, 10, 11, 10);
const RANGE = { from: new Date(2026, 10, 11), to: new Date(2026, 11, 11) };

describe('buildScheduleRows', () => {
  it('opens with the banner of the month and then lists each day', () => {
    const rows = buildScheduleRows(groupByDay([novemberBooking('a', 20, 9, 10)], RANGE, NOW), NOW);

    expect(rows.map((row) => row.key)).toEqual(['month-2026-11-01', 'day-2026-11-11', 'day-2026-11-20']);
    expect(rows[0]).toMatchObject({ kind: 'month', label: 'Noviembre' });
  });

  it('puts a banner at each change of month, with the year only when it is not the current one', () => {
    const december = new Date(2026, 11, 2, 9);
    const nextYear = new Date(2027, 0, 5, 9);
    const sections = groupByDay(
      [
        { ...novemberBooking('dec', 20, 9, 10), sessionStart: december.toISOString(), sessionEnd: new Date(2026, 11, 2, 10).toISOString() },
        { ...novemberBooking('jan', 20, 9, 10), sessionStart: nextYear.toISOString(), sessionEnd: new Date(2027, 0, 5, 10).toISOString() },
      ],
      { from: new Date(2026, 10, 11), to: new Date(2027, 1, 1) },
      NOW,
    );

    const banners = buildScheduleRows(sections, NOW).filter((row) => row.kind === 'month');

    expect(banners.map((row) => row.kind === 'month' && row.label)).toEqual(['Noviembre', 'Diciembre', 'Enero 2027']);
  });

  it('is empty when there are no days', () => {
    expect(buildScheduleRows([], NOW)).toEqual([]);
  });

  it('carries the section of each day row, to draw it', () => {
    const rows = buildScheduleRows(groupByDay([novemberBooking('a', 11, 9, 10)], RANGE, NOW), NOW);
    const day = rows.find((row) => row.kind === 'day');

    expect(day?.kind === 'day' && day.section.isToday).toBe(true);
    expect(day?.kind === 'day' && day.section.bookings.map((item) => item.id)).toEqual(['a']);
  });
});

describe('pageToLoad', () => {
  const metrics = { viewport: 600, content: 3000 };

  it('asks for the days before when the top is near', () => {
    expect(pageToLoad({ ...metrics, offset: 100 }, 400)).toBe('start');
    expect(pageToLoad({ ...metrics, offset: 400 }, 400)).toBe('start');
  });

  it('asks for the days after when the bottom is near', () => {
    expect(pageToLoad({ ...metrics, offset: 2100 }, 400)).toBe('end');
    expect(pageToLoad({ ...metrics, offset: 2000 }, 400)).toBe('end');
  });

  it('asks for nothing in the middle', () => {
    expect(pageToLoad({ ...metrics, offset: 1200 }, 400)).toBeNull();
  });

  it('asks for nothing when the content fits on the screen: there is nothing to scroll, the buttons at the ends are the way', () => {
    expect(pageToLoad({ viewport: 600, content: 500, offset: 0 }, 400)).toBeNull();
    expect(pageToLoad({ viewport: 600, content: 600, offset: 0 }, 400)).toBeNull();
  });
});

describe('labels', () => {
  it('names the five views', () => {
    expect(AGENDA_VIEW_LABELS).toEqual({ schedule: 'Agenda', day: 'Día', threeDays: '3 días', week: 'Semana', month: 'Mes' });
  });

  it('writes the weekday in three letters without a dot', () => {
    expect(weekdayShort(new Date(2026, 10, 11))).toBe('mié');
    expect(weekdayShort(new Date(2026, 10, 8))).toBe('dom');
  });

  it('writes the hours of a booking in the time of the phone', () => {
    const item = novemberBooking('a', 11, 15, 17);

    expect(formatBookingHours(item)).toBe(`${formatTime(atHour(11, 15))} – ${formatTime(atHour(11, 17))}`);
  });

  it('writes what a screen reader says about a booking: who, what, when and in what state', () => {
    const item = novemberBooking('a', 11, 15, 17, 'Tentative');

    expect(describeBookingForReader(item)).toBe(
      `Reserva de Client a, Retrato Familiar, de ${formatTime(atHour(11, 15))} a ${formatTime(atHour(11, 17))}, tentativa`,
    );
  });
});

describe('views', () => {
  it('knows which views have a screen', () => {
    expect(isViewImplemented('schedule')).toBe(true);
    expect(isViewImplemented('week')).toBe(true);
    expect(isViewImplemented('month')).toBe(false);
  });

  it('falls back to the list for a view that has no screen yet', () => {
    expect(effectiveAgendaView('schedule')).toBe('schedule');
    expect(effectiveAgendaView('month')).toBe('schedule');
  });
});
