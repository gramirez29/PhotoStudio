import { formatDateTime, formatMoney } from '../format';

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
