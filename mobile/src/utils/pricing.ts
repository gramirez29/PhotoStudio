import { EXTRAS, MAX_EXTRA_QUANTITY, PACKAGES } from '../config/pricing';
import type { PackageTier } from '../types/api/booking';
import type { ExtraOption, PackageOption } from '../types/config/pricing.types';
import type { BookingTotals, SelectedExtra } from '../types/utils/pricing.types';
import { formatMoney } from './format';
import { parseOptionalAmount, parsePrice } from './price';

/** Currency every amount of the booking form is in until the photographer can configure one. */
const PRICING_CURRENCY = 'CRC';

/** Names of the packages, in the order of the drop-down. */
export const PACKAGE_LABELS: readonly string[] = PACKAGES.map((option) => option.label);

/**
 * Finds the package whose name was picked in the drop-down.
 * @param label Name shown in the drop-down.
 * @returns The package, or undefined when no package has that name.
 */
export function findPackageByLabel(label: string): PackageOption | undefined {
  return PACKAGES.find((option) => option.label === label);
}

/**
 * Finds the package of a tier.
 * @param tier Tier to look for.
 * @returns The package, or undefined when the configuration has none for it.
 */
export function findPackage(tier: PackageTier): PackageOption | undefined {
  return PACKAGES.find((option) => option.tier === tier);
}

/**
 * Name of a package for the drop-down.
 * @param tier Tier chosen, or null when none is.
 * @returns The name, or an empty string when none is chosen.
 */
export function packageLabel(tier: PackageTier | null): string {
  return tier === null ? '' : (findPackage(tier)?.label ?? '');
}

/**
 * Works out the price field after another package is picked. A package with a price fills it in. A package without one clears
 * the field only when it still holds the price the previous package put there, so what the photographer typed is never lost.
 * @param currentPrice Price field as it is now.
 * @param previous Package that was chosen before, or null.
 * @param next Package just chosen.
 * @returns The new text of the price field.
 */
export function priceAfterPackageChange(currentPrice: string, previous: PackageTier | null, next: PackageOption): string {
  if (next.price !== null) {
    return String(next.price);
  }

  const previousPrice = previous === null ? null : (findPackage(previous)?.price ?? null);
  return previousPrice !== null && parsePrice(currentPrice) === previousPrice ? '' : currentPrice;
}

/**
 * Units of an extra in the selection.
 * @param selection Extras chosen.
 * @param code Code of the extra.
 * @returns The quantity, zero when the extra is not chosen.
 */
export function quantityOf(selection: readonly SelectedExtra[], code: string): number {
  return selection.find((extra) => extra.code === code)?.quantity ?? 0;
}

/**
 * Sets how many units of an extra are chosen. A quantity of zero removes it; the quantity never goes above the maximum.
 * @param selection Extras chosen now.
 * @param option Extra to change.
 * @param quantity New number of units.
 * @returns The new selection, in the order of the configured extras.
 */
export function withQuantity(selection: readonly SelectedExtra[], option: ExtraOption, quantity: number): readonly SelectedExtra[] {
  const clamped = Math.min(Math.max(Math.trunc(quantity), 0), MAX_EXTRA_QUANTITY);
  const others = selection.filter((extra) => extra.code !== option.code);
  const next = clamped === 0 ? others : [...others, { code: option.code, name: option.label, unitPrice: option.unitPrice, quantity: clamped }];
  const order = (code: string): number => EXTRAS.findIndex((extra) => extra.code === code);
  return [...next].sort((a, b) => order(a.code) - order(b.code));
}

/**
 * Sum of the extras chosen.
 * @param selection Extras chosen.
 * @returns The total in colones.
 */
export function extrasTotal(selection: readonly SelectedExtra[]): number {
  return selection.reduce((sum, extra) => sum + extra.unitPrice * extra.quantity, 0);
}

/**
 * Works out what the booking costs from what the form holds. Fields that are empty or not valid count as zero here; the
 * validation of the form is what reports them.
 * @param price Price field as typed.
 * @param selection Extras chosen.
 * @param discount Discount field as typed.
 * @returns The amounts, in colones.
 */
export function computeTotals(price: string, selection: readonly SelectedExtra[], discount: string): BookingTotals {
  const packagePrice = parsePrice(price) ?? 0;
  const extras = extrasTotal(selection);
  const discounted = parseOptionalAmount(discount) ?? 0;
  return { packagePrice, extrasTotal: extras, discount: discounted, total: Math.max(packagePrice + extras - discounted, 0) };
}

/**
 * Formats an amount in colones.
 * @param amount Amount in colones.
 * @returns The formatted amount, for example "₡35 000,00".
 */
export function formatColones(amount: number): string {
  return formatMoney({ amount, currency: PRICING_CURRENCY });
}
