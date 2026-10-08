/** Country code added to local phone numbers typed with 8 digits (Costa Rica). */
export const DEFAULT_COUNTRY_CODE = '+506';

/**
 * Normalizes a phone number to international format. Eight local digits get {@link DEFAULT_COUNTRY_CODE}.
 * @param raw Phone as typed (spaces, dashes and parentheses are ignored).
 * @returns The number like `+50688881111`, or null when it cannot be a phone number.
 */
export function normalizePhone(raw: string): string | null {
  const trimmed = raw.trim();
  const digits = trimmed.replace(/\D/g, '');

  if (trimmed.startsWith('+')) {
    return digits.length >= 8 && digits.length <= 15 ? `+${digits}` : null;
  }

  if (digits.length === 8) {
    return `${DEFAULT_COUNTRY_CODE}${digits}`;
  }

  const codeDigits = DEFAULT_COUNTRY_CODE.slice(1);
  if (digits.length === 8 + codeDigits.length && digits.startsWith(codeDigits)) {
    return `+${digits}`;
  }

  return null;
}
