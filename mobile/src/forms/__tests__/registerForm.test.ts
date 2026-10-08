import { EMPTY_REGISTER_VALUES, validateRegisterForm } from '../registerForm';
import type { RegisterFormValues } from '../../types/forms/registerForm.types';

/**
 * Builds valid form values, with any field replaced.
 * @param overrides Values to replace.
 * @returns The values.
 */
function valid(overrides: Partial<RegisterFormValues> = {}): RegisterFormValues {
  return {
    name: 'Ana Pérez',
    phone: '7018-9220',
    email: 'ana@example.com',
    username: 'ana.photo',
    password: 'a long password',
    confirmPassword: 'a long password',
    ...overrides,
  };
}

describe('validateRegisterForm', () => {
  it('builds the request with trimmed name, lower-case username and an international phone', () => {
    const result = validateRegisterForm(valid({ name: '  Ana Pérez ', username: '  Ana.Photo ', phone: '7018-9220', email: '  Ana@Example.COM ' }));

    expect(result).toEqual({
      ok: true,
      request: { username: 'ana.photo', email: 'ana@example.com', password: 'a long password', name: 'Ana Pérez', phone: '+50670189220' },
    });
  });

  it('keeps a phone typed with a country code', () => {
    const result = validateRegisterForm(valid({ phone: '+1 305 555 0100' }));

    expect(result.ok && result.request.phone).toBe('+13055550100');
  });

  it('reports every invalid field at once when the form is empty', () => {
    const result = validateRegisterForm(EMPTY_REGISTER_VALUES);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(Object.keys(result.errors).sort()).toEqual(['email', 'name', 'password', 'phone', 'username']);
    }
  });

  it.each(['ab', 'ana smith', 'ana@mail.com', '.ana', 'ana.', '-ana', 'ánä', 'x'.repeat(31)])('rejects the username %p', (username) => {
    const result = validateRegisterForm(valid({ username }));

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.errors.username).toBeDefined();
    }
  });

  it.each(['abc', 'a.b-c_d', '007', 'x'.repeat(30)])('accepts the username %p', (username) => {
    expect(validateRegisterForm(valid({ username })).ok).toBe(true);
  });

  it.each(['ana', 'ana@', '@example.com', 'ana@example', 'ana@.com', 'ana@@example.com', 'a na@example.com', 'ana@example.', 'ana@example..com'])('rejects the email %p', (email) => {
    const result = validateRegisterForm(valid({ email }));

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.errors.email).toBeDefined();
    }
  });

  it.each(['ana@example.com', 'ana.photo+work@mail.example.co.cr', 'A@B.CO'])('accepts the email %p', (email) => {
    expect(validateRegisterForm(valid({ email })).ok).toBe(true);
  });

  it('asks for the email when it is blank, and rejects one that is too long', () => {
    const blank = validateRegisterForm(valid({ email: '   ' }));
    const long = validateRegisterForm(valid({ email: `${'a'.repeat(250)}@b.co` }));

    expect(blank.ok === false && blank.errors.email).toBe('Escribe tu correo.');
    expect(long.ok === false && long.errors.email).toContain('correo válido');
  });

  it('rejects a name that is blank or too long', () => {
    const blank = validateRegisterForm(valid({ name: '   ' }));
    const long = validateRegisterForm(valid({ name: 'x'.repeat(101) }));

    expect(blank.ok === false && blank.errors.name).toBe('Escribe tu nombre.');
    expect(long.ok === false && long.errors.name).toContain('100');
  });

  it('rejects a phone that cannot be a phone number', () => {
    const result = validateRegisterForm(valid({ phone: '12' }));

    expect(result.ok === false && result.errors.phone).toContain('teléfono válido');
  });

  it('rejects a password that is too short or too long', () => {
    const short = validateRegisterForm(valid({ password: 'short', confirmPassword: 'short' }));
    const long = validateRegisterForm(valid({ password: 'x'.repeat(129), confirmPassword: 'x'.repeat(129) }));

    expect(short.ok === false && short.errors.password).toContain('8');
    expect(long.ok === false && long.errors.password).toContain('128');
  });

  it('accepts a password of exactly the limits', () => {
    expect(validateRegisterForm(valid({ password: 'x'.repeat(8), confirmPassword: 'x'.repeat(8) })).ok).toBe(true);
    expect(validateRegisterForm(valid({ password: 'x'.repeat(128), confirmPassword: 'x'.repeat(128) })).ok).toBe(true);
  });

  it('asks to repeat the password when the two do not match', () => {
    const result = validateRegisterForm(valid({ confirmPassword: 'a long passwore' }));

    expect(result.ok === false && result.errors.confirmPassword).toBe('Las contraseñas no coinciden.');
  });

  it('does not trim the password', () => {
    const result = validateRegisterForm(valid({ password: '  spaced out  ', confirmPassword: '  spaced out  ' }));

    expect(result.ok && result.request.password).toBe('  spaced out  ');
  });
});
