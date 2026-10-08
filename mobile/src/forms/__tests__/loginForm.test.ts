import { EMPTY_LOGIN_VALUES, validateLoginForm } from '../loginForm';

describe('validateLoginForm', () => {
  it('returns the credentials with the email trimmed and the password untouched', () => {
    const result = validateLoginForm({ email: '  ana@example.com ', password: ' pass word ' });

    expect(result).toEqual({ ok: true, email: 'ana@example.com', password: ' pass word ' });
  });

  it('asks for both fields when the form is empty', () => {
    const result = validateLoginForm(EMPTY_LOGIN_VALUES);

    expect(result).toEqual({ ok: false, errors: { email: 'Escribe tu email.', password: 'Escribe tu contraseña.' } });
  });

  it('treats a blank email as missing', () => {
    const result = validateLoginForm({ email: '   ', password: 'secret' });

    expect(result).toEqual({ ok: false, errors: { email: 'Escribe tu email.' } });
  });

  it('rejects an email without an at sign', () => {
    const result = validateLoginForm({ email: 'ana.example.com', password: 'secret' });

    expect(result).toEqual({ ok: false, errors: { email: 'Escribe un email válido.' } });
  });

  it('does not trim away a password made of spaces', () => {
    const result = validateLoginForm({ email: 'ana@example.com', password: '   ' });

    expect(result.ok).toBe(true);
  });
});
