import { DEFAULT_COUNTRY_CODE, normalizePhone } from '../phone';

describe('normalizePhone', () => {
  it('adds the country code to eight local digits, ignoring separators', () => {
    expect(normalizePhone('8888 1111')).toBe('+50688881111');
    expect(normalizePhone('(8888)-1111')).toBe('+50688881111');
  });

  it('uses the default country code', () => {
    expect(normalizePhone('88881111')).toBe(`${DEFAULT_COUNTRY_CODE}88881111`);
  });

  it('keeps an international number', () => {
    expect(normalizePhone('+1 305 555 0100')).toBe('+13055550100');
  });

  it('accepts the country code typed without the plus sign', () => {
    expect(normalizePhone('506 8888 1111')).toBe('+50688881111');
  });

  it('rejects numbers that are too short or have no recognizable format', () => {
    expect(normalizePhone('')).toBeNull();
    expect(normalizePhone('12345')).toBeNull();
    expect(normalizePhone('+123')).toBeNull();
    expect(normalizePhone('123456789')).toBeNull();
  });
});
