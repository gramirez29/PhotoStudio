import type { LoginFormResult, LoginFormValues } from '../types/forms/loginForm.types';

/** Values of an empty login form. */
export const EMPTY_LOGIN_VALUES: LoginFormValues = { email: '', password: '' };

/**
 * Validates the login form and, when it is valid, returns the credentials to send. The email is trimmed; the password is
 * sent exactly as typed, because spaces can be part of it. The backend is the one that checks whether they are correct.
 * @param values Values of the form.
 * @returns The credentials, or the validation errors by field.
 */
export function validateLoginForm(values: LoginFormValues): LoginFormResult {
  const email = values.email.trim();
  const errors: { email?: string; password?: string } = {};

  if (email.length === 0) {
    errors.email = 'Escribe tu email.';
  } else if (!email.includes('@')) {
    errors.email = 'Escribe un email válido.';
  }

  if (values.password.length === 0) {
    errors.password = 'Escribe tu contraseña.';
  }

  if (errors.email !== undefined || errors.password !== undefined) {
    return { ok: false, errors };
  }

  return { ok: true, email, password: values.password };
}
