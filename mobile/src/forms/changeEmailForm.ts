import type { ChangeEmailFormResult } from '../types/forms/changeEmailForm.types';
import { normalizeEmail } from '../utils/email';

/**
 * Validates the address typed to replace the email of the account before sending it. The backend decides whether another
 * account uses it; this only avoids a round trip for something that cannot be an email.
 * @param raw What the user typed.
 * @param current The address the account has now, in any case.
 * @returns The request, or the message to show.
 */
export function validateChangeEmailForm(raw: string, current: string): ChangeEmailFormResult {
  const email = normalizeEmail(raw);
  if (email === null) {
    return { ok: false, error: 'Escribe un correo válido, por ejemplo nombre@correo.com.' };
  }

  if (email === normalizeEmail(current)) {
    return { ok: false, error: 'Ese es el correo que ya tienes. Escribe el correcto.' };
  }

  return { ok: true, request: { email } };
}
