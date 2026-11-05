import type {
  ConfirmResetFormResult,
  RecoverPasswordFormErrors,
  RecoverPasswordFormValues,
  RequestResetFormResult,
} from '../types/forms/recoverPasswordForm.types';
import { MAX_PASSWORD_LENGTH, MIN_PASSWORD_LENGTH } from './registerForm';
import { EMAIL_CODE_LENGTH, formatCodeInput } from './verifyEmailForm';

/** Values of an empty recovery form. */
export const EMPTY_RECOVER_PASSWORD_VALUES: RecoverPasswordFormValues = {
  identifier: '',
  code: '',
  newPassword: '',
  confirmPassword: '',
};

/** Longest text accepted as a username or an email: the longest email. */
export const MAX_IDENTIFIER_LENGTH = 254;

/**
 * Validates the first step, the account the user wants to recover. The backend decides whether it exists and never says, so this
 * only avoids sending nothing.
 * @param identifier Username or email as typed.
 * @returns The request, or the validation messages.
 */
export function validateRequestResetForm(identifier: string): RequestResetFormResult {
  const trimmed = identifier.trim();
  if (trimmed.length === 0) {
    return { ok: false, errors: { identifier: 'Escribe tu usuario o tu correo.' } };
  }

  if (trimmed.length > MAX_IDENTIFIER_LENGTH) {
    return { ok: false, errors: { identifier: 'Ese texto es demasiado largo.' } };
  }

  return { ok: true, request: { identifier: trimmed } };
}

/**
 * Validates the second step, the code and the new password. Passwords are sent exactly as typed, because spaces can be part of
 * them. The backend enforces the same rules and is the one that checks the code.
 * @param identifier Username or email typed in the first step.
 * @param values Values of the form.
 * @returns The request, or the validation messages by field.
 */
export function validateConfirmResetForm(identifier: string, values: RecoverPasswordFormValues): ConfirmResetFormResult {
  const errors: { -readonly [Field in keyof RecoverPasswordFormValues]?: string } = {};
  const code = formatCodeInput(values.code);

  if (code.length !== EMAIL_CODE_LENGTH) {
    errors.code = `Escribe los ${EMAIL_CODE_LENGTH} dígitos del código.`;
  }

  if (values.newPassword.length < MIN_PASSWORD_LENGTH) {
    errors.newPassword = `La contraseña debe tener al menos ${MIN_PASSWORD_LENGTH} caracteres.`;
  } else if (values.newPassword.length > MAX_PASSWORD_LENGTH) {
    errors.newPassword = `La contraseña no puede tener más de ${MAX_PASSWORD_LENGTH} caracteres.`;
  } else if (values.confirmPassword !== values.newPassword) {
    errors.confirmPassword = 'Las contraseñas no coinciden.';
  }

  if (Object.keys(errors).length > 0) {
    const failed: RecoverPasswordFormErrors = errors;
    return { ok: false, errors: failed };
  }

  return { ok: true, request: { identifier: identifier.trim(), code, newPassword: values.newPassword } };
}
