import { dayNumber } from '../dates';
import { bookingDays, bookingInterval, bookingsByDay, sortByStart } from '../intervals';
import { atHour, booking, novemberBooking } from '../__fixtures__/agendaFixtures';

describe('bookingInterval', () => {
  it('reads the start and the end as instants', () => {
    expect(bookingInterval(novemberBooking('a', 11, 9, 11))).toEqual({ start: atHour(11, 9).getTime(), end: atHour(11, 11).getTime() });
  });
});

describe('bookingDays', () => {
  it('is one day for a session inside a day', () => {
    expect(bookingDays(novemberBooking('a', 11, 9, 11))).toEqual([dayNumber(atHour(11, 0))]);
  });

  it('lists each day a session that goes through midnight touches', () => {
    const overnight = booking('night', atHour(11, 22), atHour(12, 2));

    expect(bookingDays(overnight)).toEqual([dayNumber(atHour(11, 0)), dayNumber(atHour(12, 0))]);
  });

  it('does not count the next day when the session ends exactly at midnight', () => {
    expect(bookingDays(booking('a', atHour(11, 22), atHour(12, 0)))).toEqual([dayNumber(atHour(11, 0))]);
  });

  it('is empty when the dates cannot be read or the session does not last', () => {
    expect(bookingDays({ ...novemberBooking('a', 11, 9, 10), sessionStart: 'nope' })).toEqual([]);
    expect(bookingDays(booking('a', atHour(11, 10), atHour(11, 10)))).toEqual([]);
  });
});

describe('sortByStart', () => {
  it('orders by start and then by identifier, without changing the original list', () => {
    const list = [novemberBooking('c', 11, 11, 12), novemberBooking('b', 11, 9, 10), novemberBooking('a', 11, 9, 10)];

    expect(sortByStart(list).map((item) => item.id)).toEqual(['a', 'b', 'c']);
    expect(list.map((item) => item.id)).toEqual(['c', 'b', 'a']);
  });
});

describe('bookingsByDay', () => {
  it('groups the bookings by the days they touch, in order of start', () => {
    const grouped = bookingsByDay([
      novemberBooking('late', 11, 15, 16),
      novemberBooking('early', 11, 9, 10),
      booking('overnight', atHour(11, 22), atHour(12, 2)),
    ]);

    expect(grouped.get(dayNumber(atHour(11, 0)))?.map((item) => item.id)).toEqual(['early', 'late', 'overnight']);
    expect(grouped.get(dayNumber(atHour(12, 0)))?.map((item) => item.id)).toEqual(['overnight']);
    expect(grouped.get(dayNumber(atHour(13, 0)))).toBeUndefined();
  });
});
