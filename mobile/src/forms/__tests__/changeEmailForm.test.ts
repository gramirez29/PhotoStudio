import { validateChangeEmailForm } from '../changeEmailForm';

describe('validateChangeEmailForm', () => {
  it('returns the request with the address trimmed and in lower case', () => {
    expect(validateChangeEmailForm('  Ana@Gmail.com ', 'ana@gmial.com')).toEqual({ ok: true, request: { email: 'ana@gmail.com' } });
  });

  it.each(['', 'ana', 'ana@', 'ana@gmail', 'ana @gmail.com'])('asks for a valid address when it is "%s"', (typed) => {
    expect(validateChangeEmailForm(typed, 'ana@gmial.com')).toEqual({
      ok: false,
      error: 'Escribe un correo válido, por ejemplo nombre@correo.com.',
    });
  });

  it('refuses the address the account already has, however it is written', () => {
    expect(validateChangeEmailForm(' ANA@gmial.com', 'ana@gmial.com')).toEqual({
      ok: false,
      error: 'Ese es el correo que ya tienes. Escribe el correcto.',
    });
  });
});
