import type { LoginFormResult, LoginFormValues } from '../types/forms/loginForm.types';

/** Values of an empty login form. */
export const EMPTY_LOGIN_VALUES: LoginFormValues = { username: '', password: '' };

/**
 * Validates the login form and, when it is valid, returns the credentials to send. The username is trimmed and lower-cased
 * (usernames are case-insensitive); the password is sent exactly as typed, because spaces can be part of it. The backend is
 * the one that checks whether they are correct.
 * @param values Values of the form.
 * @returns The credentials, or the validation errors by field.
 */
export function validateLoginForm(values: LoginFormValues): LoginFormResult {
  const username = values.username.trim().toLowerCase();
  const errors: { username?: string; password?: string } = {};

  if (username.length === 0) {
    errors.username = 'Escribe tu usuario.';
  }

  if (values.password.length === 0) {
    errors.password = 'Escribe tu contraseña.';
  }

  if (errors.username !== undefined || errors.password !== undefined) {
    return { ok: false, errors };
  }

  return { ok: true, username, password: values.password };
}
