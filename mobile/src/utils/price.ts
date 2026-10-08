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
