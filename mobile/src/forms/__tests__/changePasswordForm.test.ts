import { EMPTY_CHANGE_PASSWORD_VALUES, validateChangePasswordForm } from '../changePasswordForm';

describe('validateChangePasswordForm', () => {
  it('returns the request with both passwords exactly as typed', () => {
    const result = validateChangePasswordForm({
      currentPassword: ' old password ',
      newPassword: ' new password ',
      confirmPassword: ' new password ',
    });

    expect(result).toEqual({ ok: true, request: { currentPassword: ' old password ', newPassword: ' new password ' } });
  });

  it('asks for the current password and a long enough new one when the form is empty', () => {
    const result = validateChangePasswordForm(EMPTY_CHANGE_PASSWORD_VALUES);

    expect(result).toEqual({
      ok: false,
      errors: {
        currentPassword: 'Escribe tu contraseña actual.',
        newPassword: 'La contraseña nueva debe tener al menos 8 caracteres.',
      },
    });
  });

  it('rejects a new password longer than the backend accepts', () => {
    const tooLong = 'a'.repeat(129);

    const result = validateChangePasswordForm({ currentPassword: 'old password', newPassword: tooLong, confirmPassword: tooLong });

    expect(result).toEqual({ ok: false, errors: { newPassword: 'La contraseña nueva no puede tener más de 128 caracteres.' } });
  });

  it('rejects keeping the same password', () => {
    const result = validateChangePasswordForm({
      currentPassword: 'same password',
      newPassword: 'same password',
      confirmPassword: 'same password',
    });

    expect(result).toEqual({ ok: false, errors: { newPassword: 'La contraseña nueva debe ser distinta de la actual.' } });
  });

  it('rejects a confirmation that does not match', () => {
    const result = validateChangePasswordForm({
      currentPassword: 'old password',
      newPassword: 'new password',
      confirmPassword: 'new passwort',
    });

    expect(result).toEqual({ ok: false, errors: { confirmPassword: 'Las contraseñas no coinciden.' } });
  });
});
