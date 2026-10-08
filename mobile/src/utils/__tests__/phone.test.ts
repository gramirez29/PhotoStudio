import { DEFAULT_COUNTRY_CODE, formatPhoneInput, normalizePhone } from '../phone';

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

describe('normalizePhone with the displayed format', () => {
  it('stores the number exactly as before when it is shown as 0000-0000', () => {
    expect(normalizePhone('7018-9220')).toBe('+50670189220');
  });
});

describe('formatPhoneInput', () => {
  it('shows nothing for an empty field and the digits as they are up to four', () => {
    expect(formatPhoneInput('')).toBe('');
    expect(formatPhoneInput('7')).toBe('7');
    expect(formatPhoneInput('7018')).toBe('7018');
  });

  it('adds the hyphen after the fourth digit', () => {
    expect(formatPhoneInput('70189')).toBe('7018-9');
    expect(formatPhoneInput('70189220')).toBe('7018-9220');
  });

  it('leaves a number that is already formatted as it is', () => {
    expect(formatPhoneInput('7018-9220')).toBe('7018-9220');
  });

  it('drops the hyphen when the photographer deletes back to four digits', () => {
    expect(formatPhoneInput('7018-')).toBe('7018');
  });

  it('ignores spaces, parentheses and letters', () => {
    expect(formatPhoneInput('7018 9220')).toBe('7018-9220');
    expect(formatPhoneInput('(7018) 9220')).toBe('7018-9220');
    expect(formatPhoneInput('70a18b')).toBe('7018');
  });

  it('cuts a local number at eight digits', () => {
    expect(formatPhoneInput('7018922055')).toBe('7018-9220');
  });

  it('drops the country code typed or pasted before a full local number', () => {
    expect(formatPhoneInput('50670189220')).toBe('7018-9220');
    expect(formatPhoneInput('506 7018-9220')).toBe('7018-9220');
  });

  it('does not mistake a local number that starts with 506 for a country code', () => {
    expect(formatPhoneInput('50612345')).toBe('5061-2345');
  });

  it('leaves an international number as typed', () => {
    expect(formatPhoneInput('+1 305 555 0100')).toBe('+1 305 555 0100');
    expect(formatPhoneInput('+50670189220')).toBe('+50670189220');
  });

  it('produces a value that normalizePhone turns into the stored number', () => {
    expect(normalizePhone(formatPhoneInput('506 7018 9220'))).toBe('+50670189220');
    expect(normalizePhone(formatPhoneInput('70189220'))).toBe('+50670189220');
  });
});
