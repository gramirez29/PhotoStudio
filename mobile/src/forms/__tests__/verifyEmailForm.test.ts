import { EMAIL_CODE_LENGTH, formatCodeInput, validateVerifyEmailForm } from '../verifyEmailForm';

describe('formatCodeInput', () => {
  it('keeps only the digits, so a code pasted with spaces or a line break still fits', () => {
    expect(formatCodeInput('123 456')).toBe('123456');
    expect(formatCodeInput('12-34\n56')).toBe('123456');
    expect(formatCodeInput('abc')).toBe('');
  });

  it('cuts the text to the length of the code', () => {
    expect(formatCodeInput('1234567890')).toBe('123456');
    expect(EMAIL_CODE_LENGTH).toBe(6);
  });
});

describe('validateVerifyEmailForm', () => {
  it('returns the request with the six digits', () => {
    expect(validateVerifyEmailForm('123456')).toEqual({ ok: true, request: { code: '123456' } });
    expect(validateVerifyEmailForm(' 123 456 ')).toEqual({ ok: true, request: { code: '123456' } });
  });

  it.each(['', '12345', 'abcdef', '12 34'])('asks for the six digits when the code is "%s"', (code) => {
    expect(validateVerifyEmailForm(code)).toEqual({ ok: false, error: 'Escribe los 6 dígitos del código.' });
  });
});
