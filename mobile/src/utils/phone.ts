/** Country code added to local phone numbers typed with 8 digits (Costa Rica). */
export const DEFAULT_COUNTRY_CODE = '+506';

/** Number of digits of a local (Costa Rica) phone number. */
export const LOCAL_PHONE_DIGITS = 8;

/** Number of digits a local phone shows before the hyphen, as in 7018-9220. */
const LOCAL_PHONE_GROUP = 4;

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

/**
 * Formats a phone number while it is typed so it reads like `7018-9220`. It only changes how the number is shown: the
 * stored value is still built from the digits by {@link normalizePhone}.
 * - A number that starts with `+` is international and is left as typed.
 * - Otherwise only the digits are kept, a leading country code (`506`) typed before a full local number is dropped, and
 *   the number is cut at {@link LOCAL_PHONE_DIGITS} digits with a hyphen after the fourth.
 * @param raw Text of the field after the last keystroke.
 * @returns The text to show in the field.
 */
export function formatPhoneInput(raw: string): string {
  if (raw.trimStart().startsWith('+')) {
    return raw;
  }

  const countryDigits = DEFAULT_COUNTRY_CODE.slice(1);
  let digits = raw.replace(/\D/g, '');
  if (digits.length > LOCAL_PHONE_DIGITS && digits.startsWith(countryDigits)) {
    digits = digits.slice(countryDigits.length);
  }

  digits = digits.slice(0, LOCAL_PHONE_DIGITS);
  if (digits.length <= LOCAL_PHONE_GROUP) {
    return digits;
  }

  return `${digits.slice(0, LOCAL_PHONE_GROUP)}-${digits.slice(LOCAL_PHONE_GROUP)}`;
}
