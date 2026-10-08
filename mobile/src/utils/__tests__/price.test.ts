import { parsePrice } from '../price';

describe('parsePrice', () => {
  it('accepts plain, grouped and colon-prefixed amounts', () => {
    expect(parsePrice('80000')).toBe(80000);
    expect(parsePrice('80 000')).toBe(80000);
    expect(parsePrice('₡80.000')).toBe(80000);
    expect(parsePrice('1,250,000')).toBe(1250000);
  });

  it('rejects empty, zero, negative or non numeric values', () => {
    expect(parsePrice('')).toBeNull();
    expect(parsePrice('0')).toBeNull();
    expect(parsePrice('-5')).toBeNull();
    expect(parsePrice('abc')).toBeNull();
    expect(parsePrice('12a')).toBeNull();
  });
});
