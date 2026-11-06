import {
  addDays,
  addMonths,
  dateFromDayNumber,
  dayNumber,
  daysInMonth,
  isSameDay,
  minutesSinceMidnight,
  startOfDay,
  startOfMonth,
  startOfWeek,
  toDateKey,
} from '../dates';

describe('dayNumber and dateFromDayNumber', () => {
  it('are the inverse of each other and give the midnight of the day', () => {
    const date = new Date(2026, 10, 11, 15, 45);

    expect(dateFromDayNumber(dayNumber(date))).toEqual(new Date(2026, 10, 11));
  });

  it('count one more for each day, across the end of a month and of a year', () => {
    expect(dayNumber(new Date(2026, 11, 1)) - dayNumber(new Date(2026, 10, 30))).toBe(1);
    expect(dayNumber(new Date(2027, 0, 1)) - dayNumber(new Date(2026, 11, 31))).toBe(1);
  });

  it('give the same number to every instant of a day', () => {
    expect(dayNumber(new Date(2026, 10, 11, 0, 0))).toBe(dayNumber(new Date(2026, 10, 11, 23, 59)));
  });
});

describe('startOfDay', () => {
  it('drops the time of day', () => {
    expect(startOfDay(new Date(2026, 10, 11, 18, 30, 12))).toEqual(new Date(2026, 10, 11));
  });
});

describe('addDays', () => {
  it('moves forward and back across the limits of a month and of a year', () => {
    expect(addDays(new Date(2026, 10, 29), 3)).toEqual(new Date(2026, 11, 2));
    expect(addDays(new Date(2026, 0, 2), -3)).toEqual(new Date(2025, 11, 30));
  });

  it('counts whole days across the changes of the clock of the zones that have them', () => {
    // 8 March and 1 November 2026 are the days the United States changes its clock; elsewhere they are ordinary days.
    expect(addDays(new Date(2026, 2, 7), 2)).toEqual(new Date(2026, 2, 9));
    expect(dayNumber(new Date(2026, 2, 9)) - dayNumber(new Date(2026, 2, 7))).toBe(2);
    expect(addDays(new Date(2026, 9, 31), 2)).toEqual(new Date(2026, 10, 2));
    expect(addDays(new Date(2026, 10, 2), -2)).toEqual(new Date(2026, 9, 31));
  });

  it('knows about the 29th of February of a leap year', () => {
    expect(addDays(new Date(2028, 1, 28), 1)).toEqual(new Date(2028, 1, 29));
    expect(addDays(new Date(2026, 1, 28), 1)).toEqual(new Date(2026, 2, 1));
  });

  it('returns a midnight even when it starts from another time of the day', () => {
    expect(addDays(new Date(2026, 10, 11, 17, 5), 1)).toEqual(new Date(2026, 10, 12));
  });
});

describe('addMonths', () => {
  it('goes to the first day of the month, so the 31st never overflows', () => {
    expect(addMonths(new Date(2026, 0, 31), 1)).toEqual(new Date(2026, 1, 1));
    expect(addMonths(new Date(2026, 10, 15), -11)).toEqual(new Date(2025, 11, 1));
    expect(addMonths(new Date(2026, 10, 15), 2)).toEqual(new Date(2027, 0, 1));
  });
});

describe('startOfWeek', () => {
  it('starts the week on Sunday by default', () => {
    expect(startOfWeek(new Date(2026, 10, 11))).toEqual(new Date(2026, 10, 8));
    expect(startOfWeek(new Date(2026, 10, 8))).toEqual(new Date(2026, 10, 8));
    expect(startOfWeek(new Date(2026, 10, 14))).toEqual(new Date(2026, 10, 8));
  });

  it('can start it on another day', () => {
    expect(startOfWeek(new Date(2026, 10, 11), 1)).toEqual(new Date(2026, 10, 9));
    expect(startOfWeek(new Date(2026, 10, 8), 1)).toEqual(new Date(2026, 10, 2));
  });

  it('crosses the limit of the month', () => {
    expect(startOfWeek(new Date(2026, 11, 2))).toEqual(new Date(2026, 10, 29));
  });
});

describe('startOfMonth and daysInMonth', () => {
  it('give the first day and the length of the month', () => {
    expect(startOfMonth(new Date(2026, 10, 20, 8))).toEqual(new Date(2026, 10, 1));
    expect(daysInMonth(new Date(2026, 10, 20))).toBe(30);
    expect(daysInMonth(new Date(2026, 11, 1))).toBe(31);
    expect(daysInMonth(new Date(2026, 1, 1))).toBe(28);
    expect(daysInMonth(new Date(2028, 1, 1))).toBe(29);
  });
});

describe('isSameDay', () => {
  it('compares the day, not the time', () => {
    expect(isSameDay(new Date(2026, 10, 11, 1), new Date(2026, 10, 11, 23))).toBe(true);
    expect(isSameDay(new Date(2026, 10, 11, 23, 59), new Date(2026, 10, 12, 0, 0))).toBe(false);
  });
});

describe('minutesSinceMidnight', () => {
  it('counts the minutes by the clock on the wall', () => {
    expect(minutesSinceMidnight(new Date(2026, 10, 11, 0, 0))).toBe(0);
    expect(minutesSinceMidnight(new Date(2026, 10, 11, 9, 30))).toBe(570);
    expect(minutesSinceMidnight(new Date(2026, 10, 11, 23, 59))).toBe(1439);
  });
});

describe('toDateKey', () => {
  it('writes the day as YYYY-MM-DD with the zeros', () => {
    expect(toDateKey(new Date(2026, 0, 5))).toBe('2026-01-05');
    expect(toDateKey(new Date(2026, 10, 11, 23, 59))).toBe('2026-11-11');
  });
});
