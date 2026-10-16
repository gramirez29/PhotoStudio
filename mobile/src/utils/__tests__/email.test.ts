import { MAX_EMAIL_LENGTH, normalizeEmail } from '../email';

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
