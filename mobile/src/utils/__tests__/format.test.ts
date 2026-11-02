import { formatBytes, formatDate, formatDateTime, formatMoney, formatTime } from '../format';

describe('formatBytes', () => {
  it('writes megabytes from one on and kilobytes below, in Spanish', () => {
    expect(formatBytes(4_404_019)).toBe('4,2 MB');
    expect(formatBytes(1_048_576)).toBe('1 MB');
    expect(formatBytes(870_000)).toBe('850 KB');
    expect(formatBytes(10)).toBe('1 KB');
  });
});

describe('formatMoney', () => {
  it('formats colones with the currency symbol', () => {
    const text = formatMoney({ amount: 50000, currency: 'CRC' });

    expect(text).toContain('₡');
    expect(text.replace(/\D/g, '')).toBe('5000000');
  });
});

describe('formatDateTime', () => {
  it('returns the original text when the date is invalid', () => {
    expect(formatDateTime('not-a-date')).toBe('not-a-date');
  });

  it('formats a valid ISO date', () => {
    expect(formatDateTime('2026-10-11T12:00:00+00:00')).toMatch(/2026/);
  });
});

describe('formatDate and formatTime', () => {
  it('include the day and the time of the given local date', () => {
    const date = new Date(2026, 9, 17, 15, 30);

    expect(formatDate(date)).toContain('17');
    expect(formatDate(date)).toContain('2026');
    expect(formatTime(date).replace(/\D/g, '')).toContain('30');
  });
});
