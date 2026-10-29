import type { ExtraOption, PackageOption } from '../types/config/pricing.types';

/**
 * Prices of the photographer: the packages with the price and the session length each one brings, and the extras with their
 * unit price. This is the file to edit when a price changes; nothing else in the app has a price written in it.
 *
 * A booking keeps the prices it was sold with, so changing them here never changes a booking that already exists.
 */

/**
 * Packages offered when a booking is created, in the order of the drop-down. A package with a null price or length leaves that
 * field for the photographer to fill in.
 */
export const PACKAGES: readonly PackageOption[] = [
  { tier: 'Basic', label: 'Básico', price: 35000, durationHours: 1 },
  { tier: 'Standard', label: 'Estándar', price: 65000, durationHours: 2 },
  { tier: 'Premium', label: 'Premium', price: 110000, durationHours: 3 },
  { tier: 'Custom', label: 'Custom', price: null, durationHours: null },
  { tier: 'Other', label: 'Otro', price: null, durationHours: null },
];

/**
 * Extras that can be added to a booking, in the order of the list. Their prices are still to be defined: until they are set
 * here, an extra adds nothing to the total.
 */
export const EXTRAS: readonly ExtraOption[] = [
  { code: 'extra-hour', label: 'Hora adicional', unitPrice: 0 },
  { code: 'extra-person', label: 'Persona adicional', unitPrice: 0 },
  { code: 'extra-edited-photo', label: 'Foto editada adicional', unitPrice: 0 },
  { code: 'rush-delivery', label: 'Entrega urgente', unitPrice: 0 },
  { code: 'extra-print', label: 'Impresión adicional', unitPrice: 0 },
  { code: 'makeup', label: 'Maquillaje', unitPrice: 0 },
];

/** Largest quantity of a single extra; the backend accepts the same. */
export const MAX_EXTRA_QUANTITY = 99;
