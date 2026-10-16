/**
 * Parses an amount typed in whole units of the currency (colones). Spaces, dots, commas and the colon sign are accepted as
 * grouping marks, so "50 000", "50.000" and "₡50,000" all mean fifty thousand.
 * @param raw Amount as typed.
 * @returns The amount, or null when it is empty, not a number or not greater than zero.
 */
export function parseAmount(raw: string): number | null {
  const cleaned = raw.replace(/[\s.,₡]/g, '');
  if (!/^\d+$/.test(cleaned)) {
    return null;
  }

  const amount = Number(cleaned);
  return Number.isSafeInteger(amount) && amount > 0 ? amount : null;
}

/**
 * Gets the symbol of a currency in the locale of the page, for example "₡" for CRC.
 * @param currency ISO 4217 currency code.
 * @param locale Locale to read the symbol in.
 * @returns The symbol, or the code itself when the locale has no symbol for it.
 */
export function currencySymbol(currency: string, locale: string): string {
  const parts = new Intl.NumberFormat(locale, { style: 'currency', currency }).formatToParts(0);
  return parts.find((part) => part.type === 'currency')?.value ?? currency;
}
