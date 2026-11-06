import { buildCalendarPath, createBookingsApi, parseCalendar } from '../bookingsApi';
import { createHttpClient } from '../httpClient';
import { bookingSummaryPayload, jsonResponse, stubFetch } from '../__fixtures__/testResponses';

describe('parseCalendar', () => {
  it('parses the bookings of a range in the order the backend sent them, and whether some were left out', () => {
    const second = { ...bookingSummaryPayload, id: 'second', clientName: 'Luis Mora', status: 'Tentative' };

    const calendar = parseCalendar({ items: [bookingSummaryPayload, second], truncated: false });

    expect(calendar.items.map((item) => item.clientName)).toEqual(['María Pérez', 'Luis Mora']);
    expect(calendar.items[1]?.status).toBe('Tentative');
    expect(calendar.items[0]?.sessionStart).toBe('2026-10-11T12:00:00+00:00');
    expect(calendar.truncated).toBe(false);
  });

  it('keeps the flag that says some bookings were left out', () => {
    expect(parseCalendar({ items: [], truncated: true })).toEqual({ items: [], truncated: true });
  });

  it('rejects a body that is not a calendar', () => {
    expect(() => parseCalendar([])).toThrow('calendar should be an object');
    expect(() => parseCalendar({ truncated: false })).toThrow('calendar.items should be an array');
    expect(() => parseCalendar({ items: [] })).toThrow('calendar.truncated');
    expect(() => parseCalendar({ items: [], truncated: 'no' })).toThrow('calendar.truncated');
  });

  it('points to the booking that is invalid', () => {
    expect(() => parseCalendar({ items: [bookingSummaryPayload, { ...bookingSummaryPayload, status: 'Archived' }], truncated: false })).toThrow(
      'calendar.items[1].status',
    );
  });
});

describe('buildCalendarPath', () => {
  const range = { from: '2026-11-08T06:00:00.000Z', to: '2026-11-15T06:00:00.000Z' };

  it('puts the range in the query string, with the instants encoded', () => {
    expect(buildCalendarPath(range, false)).toBe(
      '/api/bookings/calendar?from=2026-11-08T06%3A00%3A00.000Z&to=2026-11-15T06%3A00%3A00.000Z',
    );
  });

  it('asks for the bookings that are over only when it is told to', () => {
    expect(buildCalendarPath(range, true)).toBe(`${buildCalendarPath(range, false)}&includeEnded=true`);
  });
});

describe('createBookingsApi().getCalendar', () => {
  it('requests the range and parses the answer', async () => {
    const { fetchFn, urls } = stubFetch(jsonResponse(200, { items: [bookingSummaryPayload], truncated: false }));
    const api = createBookingsApi(createHttpClient('https://api.example.test', { fetchFn }));

    const calendar = await api.getCalendar({ from: '2026-11-08T06:00:00.000Z', to: '2026-11-15T06:00:00.000Z' }, true);

    expect(urls).toEqual([
      'https://api.example.test/api/bookings/calendar?from=2026-11-08T06%3A00%3A00.000Z&to=2026-11-15T06%3A00%3A00.000Z&includeEnded=true',
    ]);
    expect(calendar.items).toHaveLength(1);
  });
});
