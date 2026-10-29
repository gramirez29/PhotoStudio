import { MAX_EMAIL_LENGTH, normalizeEmail, maskEmail } from '../email';

describe('normalizeEmail', () => {
  it('trims and lower-cases a valid address', () => {
    expect(normalizeEmail('  Maria.Perez@Example.COM ')).toBe('maria.perez@example.com');
  });

  it.each(['', '   ', 'maria', 'maria@', '@example.com', 'maria@example', 'a b@example.com', 'a@@example.com'])(
    'rejects %p',
    (raw) => {
      expect(normalizeEmail(raw)).toBeNull();
    },
  );

  it('rejects an address longer than the limit', () => {
    expect(normalizeEmail(`${'a'.repeat(MAX_EMAIL_LENGTH)}@example.com`)).toBeNull();
  });
});

describe('maskEmail', () => {
  it('keeps the first letter and the domain, and hides the rest', () => {
    expect(maskEmail('ana.perez@gmail.com')).toBe('a***@gmail.com');
  });

  it('hides everything when the text has no valid shape', () => {
    expect(maskEmail('')).toBe('***');
    expect(maskEmail('no-at-sign')).toBe('***');
    expect(maskEmail('@gmail.com')).toBe('***');
  });
});
