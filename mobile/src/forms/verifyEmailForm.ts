import type { VerifyEmailFormResult } from '../types/forms/verifyEmailForm.types';

/** Digits of the code that verifies the email. */
export const EMAIL_CODE_LENGTH = 6;

/**
 * Keeps only the digits the user typed or pasted, up to the length of the code, so a code copied with spaces or a line break
 * from the email still fits.
 * @param typed Text of the field.
 * @returns The digits.
 */
export function formatCodeInput(typed: string): string {
  return typed.replace(/\D/g, '').slice(0, EMAIL_CODE_LENGTH);
}

/**
 * Validates the code before sending it. The backend decides whether it is the right one; this only avoids a round trip for
 * a code that cannot be right.
 * @param code What the user typed.
 * @returns The request, or the message to show.
 */
export function validateVerifyEmailForm(code: string): VerifyEmailFormResult {
  const digits = formatCodeInput(code);
  if (digits.length !== EMAIL_CODE_LENGTH) {
    return { ok: false, error: `Escribe los ${EMAIL_CODE_LENGTH} dígitos del código.` };
  }

  return { ok: true, request: { code: digits } };
}
