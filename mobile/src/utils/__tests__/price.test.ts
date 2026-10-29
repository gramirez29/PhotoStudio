import { parseOptionalAmount, parsePrice } from '../price';

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

describe('parseOptionalAmount', () => {
  it('reads an empty field as zero and accepts zero itself', () => {
    expect(parseOptionalAmount('')).toBe(0);
    expect(parseOptionalAmount('  ')).toBe(0);
    expect(parseOptionalAmount('0')).toBe(0);
  });

  it('accepts the same grouping marks as a price', () => {
    expect(parseOptionalAmount('5 000')).toBe(5000);
    expect(parseOptionalAmount('₡10.000')).toBe(10000);
  });

  it('rejects what is not a whole number', () => {
    expect(parseOptionalAmount('-5')).toBeNull();
    expect(parseOptionalAmount('abc')).toBeNull();
    expect(parseOptionalAmount('12a')).toBeNull();
  });
});
