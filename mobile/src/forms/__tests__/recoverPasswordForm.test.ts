import {
  EMPTY_RECOVER_PASSWORD_VALUES,
  MAX_IDENTIFIER_LENGTH,
  validateConfirmResetForm,
  validateRequestResetForm,
} from '../recoverPasswordForm';

describe('validateRequestResetForm', () => {
  it('returns the request with the text trimmed', () => {
    expect(validateRequestResetForm('  ana@example.com ')).toEqual({ ok: true, request: { identifier: 'ana@example.com' } });
  });

  it.each(['', '   '])('asks for the account when nothing is typed ("%s")', (typed) => {
    expect(validateRequestResetForm(typed)).toEqual({ ok: false, errors: { identifier: 'Escribe tu usuario o tu correo.' } });
  });

  it('refuses a text longer than any email', () => {
    expect(validateRequestResetForm('a'.repeat(MAX_IDENTIFIER_LENGTH + 1))).toEqual({
      ok: false,
      errors: { identifier: 'Ese texto es demasiado largo.' },
    });
  });
});

describe('validateConfirmResetForm', () => {
  const valid = { ...EMPTY_RECOVER_PASSWORD_VALUES, code: '123456', newPassword: 'a new password', confirmPassword: 'a new password' };

  it('returns the request with the code as digits and the password exactly as typed', () => {
    expect(validateConfirmResetForm(' ana ', { ...valid, code: '123 456', newPassword: ' spaced pass ', confirmPassword: ' spaced pass ' })).toEqual({
      ok: true,
      request: { identifier: 'ana', code: '123456', newPassword: ' spaced pass ' },
    });
  });

  it.each(['', '12345', 'abcdef'])('asks for the six digits when the code is "%s"', (code) => {
    expect(validateConfirmResetForm('ana', { ...valid, code })).toEqual({ ok: false, errors: { code: 'Escribe los 6 dígitos del código.' } });
  });

  it('asks for a password of the right length', () => {
    expect(validateConfirmResetForm('ana', { ...valid, newPassword: 'short', confirmPassword: 'short' })).toEqual({
      ok: false,
      errors: { newPassword: 'La contraseña debe tener al menos 8 caracteres.' },
    });
    expect(validateConfirmResetForm('ana', { ...valid, newPassword: 'x'.repeat(129), confirmPassword: 'x'.repeat(129) })).toEqual({
      ok: false,
      errors: { newPassword: 'La contraseña no puede tener más de 128 caracteres.' },
    });
  });

  it('asks for the same password twice', () => {
    expect(validateConfirmResetForm('ana', { ...valid, confirmPassword: 'another one' })).toEqual({
      ok: false,
      errors: { confirmPassword: 'Las contraseñas no coinciden.' },
    });
  });

  it('reports every problem at once', () => {
    const result = validateConfirmResetForm('ana', EMPTY_RECOVER_PASSWORD_VALUES);

    expect(result.ok).toBe(false);
    expect(result.ok ? {} : Object.keys(result.errors).sort()).toEqual(['code', 'newPassword']);
  });
});
