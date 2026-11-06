import { isAgendaView, parseAgendaPreferences } from '../preferences';
import { agendaTitle, monthName } from '../titles';

describe('monthName', () => {
  it('writes the month in Spanish with a capital letter', () => {
    expect(monthName(new Date(2026, 10, 11))).toBe('Noviembre');
    expect(monthName(new Date(2026, 0, 1))).toBe('Enero');
  });
});

describe('agendaTitle', () => {
  const now = new Date(2026, 10, 11);

  it('is the month of the period, without the year when it is the current one', () => {
    expect(agendaTitle('month', new Date(2026, 10, 20), now)).toBe('Noviembre');
    expect(agendaTitle('day', new Date(2026, 5, 3), now)).toBe('Junio');
  });

  it('adds the year when it is not the current one', () => {
    expect(agendaTitle('month', new Date(2027, 11, 5), now)).toBe('Diciembre 2027');
    expect(agendaTitle('schedule', new Date(2025, 0, 5), now)).toBe('Enero 2025');
  });

  it('names a week that crosses two months by the month it starts in', () => {
    // The week of Wednesday 2 December 2026 starts on Sunday 29 November.
    expect(agendaTitle('week', new Date(2026, 11, 2), now)).toBe('Noviembre');
  });
});

describe('isAgendaView', () => {
  it('accepts the five views and nothing else', () => {
    for (const view of ['schedule', 'day', 'threeDays', 'week', 'month']) {
      expect(isAgendaView(view)).toBe(true);
    }

    expect(isAgendaView('year')).toBe(false);
    expect(isAgendaView(null)).toBe(false);
    expect(isAgendaView(3)).toBe(false);
  });
});

describe('parseAgendaPreferences', () => {
  it('reads what was saved', () => {
    expect(parseAgendaPreferences('{"view":"week","showAll":true}')).toEqual({ view: 'week', showAll: true });
  });

  it.each([null, '', 'not json', '[]', 'null', '{"view":"year","showAll":true}', '{"view":"week"}', '{"view":"week","showAll":"yes"}'])(
    'discards what is not valid (%s)',
    (raw) => {
      expect(parseAgendaPreferences(raw)).toBeNull();
    },
  );
});
