import type { ChangePasswordFormResult, ChangePasswordFormValues } from '../types/forms/changePasswordForm.types';
import { MAX_PASSWORD_LENGTH, MIN_PASSWORD_LENGTH } from './registerForm';

/** Values of an empty change-password form. */
export const EMPTY_CHANGE_PASSWORD_VALUES: ChangePasswordFormValues = {
  currentPassword: '',
  newPassword: '',
  confirmPassword: '',
};

/**
 * Validates the change-password form and, when it is valid, builds the request. Passwords are sent exactly as typed,
 * because spaces can be part of them. The backend enforces the same rules and is the one that checks the current password.
 * @param values Values of the form.
 * @returns The request, or the validation errors by field.
 */
export function validateChangePasswordForm(values: ChangePasswordFormValues): ChangePasswordFormResult {
  const errors: { -readonly [Field in keyof ChangePasswordFormValues]?: string } = {};

  if (values.currentPassword.length === 0) {
    errors.currentPassword = 'Escribe tu contraseña actual.';
  }

  if (values.newPassword.length < MIN_PASSWORD_LENGTH) {
    errors.newPassword = `La contraseña nueva debe tener al menos ${MIN_PASSWORD_LENGTH} caracteres.`;
  } else if (values.newPassword.length > MAX_PASSWORD_LENGTH) {
    errors.newPassword = `La contraseña nueva no puede tener más de ${MAX_PASSWORD_LENGTH} caracteres.`;
  } else if (values.newPassword === values.currentPassword) {
    errors.newPassword = 'La contraseña nueva debe ser distinta de la actual.';
  }

  if (errors.newPassword === undefined && values.confirmPassword !== values.newPassword) {
    errors.confirmPassword = 'Las contraseñas no coinciden.';
  }

  if (Object.keys(errors).length > 0) {
    return { ok: false, errors };
  }

  return { ok: true, request: { currentPassword: values.currentPassword, newPassword: values.newPassword } };
}
