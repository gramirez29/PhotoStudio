/**
 * Parses a price typed in whole colones. Spaces, dots, commas and the colon sign are accepted as grouping marks.
 * @param raw Price as typed.
 * @returns The amount, or null when it is empty, not a number or not greater than zero.
 */
export function parsePrice(raw: string): number | null {
  const cleaned = raw.replace(/[\s.,₡]/g, '');
  if (!/^\d+$/.test(cleaned)) {
    return null;
  }

  const amount = Number(cleaned);
  return Number.isSafeInteger(amount) && amount > 0 ? amount : null;
}

/**
 * Parses an optional amount typed in whole colones, such as a discount. Same grouping marks as {@link parsePrice}, but an empty
 * field means zero and zero itself is valid.
 * @param raw Amount as typed.
 * @returns The amount (zero for an empty field), or null when it is not a number.
 */
export function parseOptionalAmount(raw: string): number | null {
  const cleaned = raw.replace(/[\s.,₡]/g, '');
  if (cleaned.length === 0) {
    return 0;
  }

  if (!/^\d+$/.test(cleaned)) {
    return null;
  }

  const amount = Number(cleaned);
  return Number.isSafeInteger(amount) ? amount : null;
}
