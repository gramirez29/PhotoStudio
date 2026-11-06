import { layoutDay } from '../layout';
import { atHour, booking, novemberBooking } from '../__fixtures__/agendaFixtures';

/** The day every test lays out: the 11th of November 2026. */
const DAY = new Date(2026, 10, 11, 12);

/**
 * Lays out a day and describes each block as "id:column/columns+span", ordered by start.
 * @param bookings Bookings to lay out.
 * @returns One text per block.
 */
function describeLayout(bookings: Parameters<typeof layoutDay>[0]): string[] {
  return layoutDay(bookings, DAY).map((event) => `${event.booking.id}:${event.column}/${event.columns}+${event.columnSpan}`);
}

describe('layoutDay', () => {
  it('puts a lone booking in one full-width column, at its minutes', () => {
    const [event] = layoutDay([novemberBooking('a', 11, 9, 11)], DAY);

    expect(event).toMatchObject({ startMinutes: 540, endMinutes: 660, visualEndMinutes: 660, column: 0, columns: 1, columnSpan: 1 });
    expect(event?.continuesBefore).toBe(false);
    expect(event?.continuesAfter).toBe(false);
  });

  it('gives a short booking the minimum height, without changing when it ends', () => {
    const [event] = layoutDay([novemberBooking('a', 11, 9, 9.25)], DAY);

    expect(event).toMatchObject({ startMinutes: 540, endMinutes: 555, visualEndMinutes: 570 });
  });

  it('ignores the bookings that do not touch the day', () => {
    const found = layoutDay(
      [novemberBooking('before', 10, 9, 11), novemberBooking('after', 12, 9, 11), novemberBooking('inside', 11, 9, 11)],
      DAY,
    );

    expect(found.map((event) => event.booking.id)).toEqual(['inside']);
  });

  it('does not count a booking that ends exactly at midnight in the next day', () => {
    const endsAtMidnight = booking('late', atHour(10, 22), atHour(11, 0));

    expect(layoutDay([endsAtMidnight], DAY)).toEqual([]);
    expect(layoutDay([endsAtMidnight], new Date(2026, 10, 10, 12)).map((event) => event.booking.id)).toEqual(['late']);
  });

  it('clips a booking that started yesterday and one that goes on tomorrow, and says so', () => {
    const startedYesterday = booking('from-yesterday', atHour(10, 22), atHour(11, 2));
    const goesOnTomorrow = booking('to-tomorrow', atHour(11, 22), atHour(12, 3));

    const [first, second] = layoutDay([startedYesterday, goesOnTomorrow], DAY);

    expect(first).toMatchObject({ startMinutes: 0, endMinutes: 120, continuesBefore: true, continuesAfter: false });
    expect(second).toMatchObject({ startMinutes: 1320, endMinutes: 1440, continuesBefore: false, continuesAfter: true });
  });

  it('keeps bookings that do not overlap in separate groups, each using the whole width', () => {
    expect(describeLayout([novemberBooking('a', 11, 9, 10), novemberBooking('b', 11, 10, 11)])).toEqual(['a:0/1+1', 'b:0/1+1']);
  });

  it('puts two bookings that overlap side by side', () => {
    expect(describeLayout([novemberBooking('a', 11, 9, 11), novemberBooking('b', 11, 10, 12)])).toEqual(['a:0/2+1', 'b:1/2+1']);
  });

  it('puts bookings that start together side by side, the longer one first', () => {
    expect(describeLayout([novemberBooking('short', 11, 9, 10), novemberBooking('long', 11, 9, 12)])).toEqual(['long:0/2+1', 'short:1/2+1']);
  });

  it('reuses a column once its booking has ended, in a chain of overlaps', () => {
    // a 9-11, b 10-12, c 11-13: a and c can share the first column, so two columns are enough for the three.
    expect(
      describeLayout([novemberBooking('a', 11, 9, 11), novemberBooking('b', 11, 10, 12), novemberBooking('c', 11, 11, 13)]),
    ).toEqual(['a:0/2+1', 'b:1/2+1', 'c:0/2+1']);
  });

  it('stretches a booking over the columns to its right that nothing uses meanwhile', () => {
    // a 9-12 first column; b and c 9-10 second and third; d 10-12 takes b's column and can use c's too, free since 10.
    const found = layoutDay(
      [
        novemberBooking('a', 11, 9, 12),
        novemberBooking('b', 11, 9, 10),
        novemberBooking('c', 11, 9, 10),
        novemberBooking('d', 11, 10, 12),
      ],
      DAY,
    );
    const byId = new Map(found.map((event) => [event.booking.id, event]));

    expect(byId.get('a')).toMatchObject({ column: 0, columns: 3, columnSpan: 1 });
    expect(byId.get('b')).toMatchObject({ column: 1, columnSpan: 1 });
    expect(byId.get('c')).toMatchObject({ column: 2, columnSpan: 1 });
    expect(byId.get('d')).toMatchObject({ column: 1, columns: 3, columnSpan: 2 });
  });

  it('counts the minimum height when deciding what overlaps: two short bookings close together are side by side', () => {
    // 9:00-9:10 is drawn until 9:30, so a booking at 9:20 would be drawn over it.
    expect(describeLayout([novemberBooking('a', 11, 9, 9 + 10 / 60), novemberBooking('b', 11, 9 + 20 / 60, 9.5)])).toEqual(['a:0/2+1', 'b:1/2+1']);
  });

  it('gives the same layout whatever the order the bookings come in', () => {
    const list = [novemberBooking('c', 11, 11, 13), novemberBooking('a', 11, 9, 11), novemberBooking('b', 11, 10, 12)];

    expect(describeLayout([...list].reverse())).toEqual(describeLayout(list));
  });

  it('reads the minutes by the clock on the wall, also on the days the clock changes', () => {
    // 8 March 2026 has 23 hours in the zones that change their clock that day; in the others it is an ordinary day.
    const day = new Date(2026, 2, 8, 12);
    const evening = booking('evening', new Date(2026, 2, 8, 22, 0), new Date(2026, 2, 8, 23, 30));

    const [event] = layoutDay([evening], day);

    expect(event).toMatchObject({ startMinutes: 22 * 60, endMinutes: 23 * 60 + 30 });
  });

  it('ignores a booking whose dates cannot be read', () => {
    const broken = { ...novemberBooking('broken', 11, 9, 10), sessionStart: 'not a date' };

    expect(layoutDay([broken, novemberBooking('ok', 11, 9, 10)], DAY).map((event) => event.booking.id)).toEqual(['ok']);
  });

  it('works with a booking that is over beside one that is not, as the switch to see everything shows them', () => {
    const cancelled = novemberBooking('cancelled', 11, 9, 11, 'Cancelled');
    const replacement = novemberBooking('replacement', 11, 10, 12, 'Confirmed');

    expect(describeLayout([cancelled, replacement])).toEqual(['cancelled:0/2+1', 'replacement:1/2+1']);
  });
});
