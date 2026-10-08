import type { RegisterFormResult, RegisterFormValues } from '../types/forms/registerForm.types';
import { normalizePhone } from '../utils/phone';

/** Values of an empty registration form. */
export const EMPTY_REGISTER_VALUES: RegisterFormValues = {
  name: '',
  phone: '',
  email: '',
  username: '',
  password: '',
  confirmPassword: '',
};

/** Most characters of an email address (the limit of an address in the SMTP standard). */
export const MAX_EMAIL_LENGTH = 254;

/** One at sign with something before it, and a domain with a dot after it; no spaces. Only the shape is checked. */
const EMAIL_PATTERN = /^[^\s@]+@[^\s@.]+(\.[^\s@.]+)+$/;

/** Fewest characters of a username (the backend enforces the same limits). */
export const MIN_USERNAME_LENGTH = 3;

/** Most characters of a username. */
export const MAX_USERNAME_LENGTH = 30;

/** Most characters of a display name. */
export const MAX_NAME_LENGTH = 100;

/** Fewest characters of a password. */
export const MIN_PASSWORD_LENGTH = 8;

/** Most characters of a password. */
export const MAX_PASSWORD_LENGTH = 128;

/** Letters, digits, dots, hyphens and underscores, starting and ending with a letter or a digit. */
const USERNAME_PATTERN = /^[a-z0-9]([a-z0-9._-]*[a-z0-9])?$/;

/**
 * Validates the registration form and, when it is valid, builds the request for the API. The same rules are enforced by the
 * backend; checking them here only saves a round trip and gives a message next to the field.
 * @param values Values of the form.
 * @returns The request, or the validation errors by field.
 */
export function validateRegisterForm(values: RegisterFormValues): RegisterFormResult {
  const errors: { -readonly [Field in keyof RegisterFormValues]?: string } = {};

  const name = values.name.trim();
  if (name.length === 0) {
    errors.name = 'Escribe tu nombre.';
  } else if (name.length > MAX_NAME_LENGTH) {
    errors.name = `El nombre no puede tener más de ${MAX_NAME_LENGTH} caracteres.`;
  }

  const phone = normalizePhone(values.phone);
  if (phone === null) {
    errors.phone = 'Escribe un teléfono válido (8 dígitos o con código de país).';
  }

  const email = values.email.trim().toLowerCase();
  if (email.length === 0) {
    errors.email = 'Escribe tu correo.';
  } else if (email.length > MAX_EMAIL_LENGTH || !EMAIL_PATTERN.test(email)) {
    errors.email = 'Escribe un correo válido, por ejemplo nombre@correo.com.';
  }

  const username = values.username.trim().toLowerCase();
  if (username.length < MIN_USERNAME_LENGTH || username.length > MAX_USERNAME_LENGTH || !USERNAME_PATTERN.test(username)) {
    errors.username = `Usa de ${MIN_USERNAME_LENGTH} a ${MAX_USERNAME_LENGTH} letras, números, puntos, guiones o guiones bajos, sin empezar ni terminar con un símbolo.`;
  }

  if (values.password.length < MIN_PASSWORD_LENGTH) {
    errors.password = `La contraseña debe tener al menos ${MIN_PASSWORD_LENGTH} caracteres.`;
  } else if (values.password.length > MAX_PASSWORD_LENGTH) {
    errors.password = `La contraseña no puede tener más de ${MAX_PASSWORD_LENGTH} caracteres.`;
  }

  if (errors.password === undefined && values.confirmPassword !== values.password) {
    errors.confirmPassword = 'Las contraseñas no coinciden.';
  }

  if (phone === null || Object.keys(errors).length > 0) {
    return { ok: false, errors };
  }

  return { ok: true, request: { username, email, password: values.password, name, phone } };
}
