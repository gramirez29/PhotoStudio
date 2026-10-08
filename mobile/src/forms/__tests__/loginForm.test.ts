import { EMPTY_LOGIN_VALUES, validateLoginForm } from '../loginForm';

describe('validateLoginForm', () => {
  it('returns the credentials with the username trimmed and in lower case, and the password untouched', () => {
    const result = validateLoginForm({ username: '  Ana.Photo ', password: ' pass word ' });

    expect(result).toEqual({ ok: true, username: 'ana.photo', password: ' pass word ' });
  });

  it('asks for both fields when the form is empty', () => {
    const result = validateLoginForm(EMPTY_LOGIN_VALUES);

    expect(result).toEqual({ ok: false, errors: { username: 'Escribe tu usuario.', password: 'Escribe tu contraseña.' } });
  });

  it('treats a blank username as missing', () => {
    const result = validateLoginForm({ username: '   ', password: 'secret' });

    expect(result).toEqual({ ok: false, errors: { username: 'Escribe tu usuario.' } });
  });

  it('does not trim away a password made of spaces', () => {
    const result = validateLoginForm({ username: 'ana', password: '   ' });

    expect(result.ok).toBe(true);
  });
});
