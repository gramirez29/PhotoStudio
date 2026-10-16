/** Most characters of an email address (the limit of an address in the SMTP standard). */
export const MAX_EMAIL_LENGTH = 254;

/** One at sign with something before it, and a domain with a dot after it; no spaces. Only the shape is checked. */
const EMAIL_PATTERN = /^[^\s@]+@[^\s@.]+(\.[^\s@.]+)+$/;

/**
 * Normalizes an email address the way the backend does: trimmed and in lower case.
 * @param raw Email as typed.
 * @returns The normalized address, or null when it has no valid shape.
 */
export function normalizeEmail(raw: string): string | null {
  const email = raw.trim().toLowerCase();
  return email.length > 0 && email.length <= MAX_EMAIL_LENGTH && EMAIL_PATTERN.test(email) ? email : null;
}
