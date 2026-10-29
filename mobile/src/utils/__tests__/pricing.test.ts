import { EXTRAS, MAX_EXTRA_QUANTITY, PACKAGES } from '../../config/pricing';
import type { SelectedExtra } from '../../types/utils/pricing.types';
import {
  computeTotals,
  extrasTotal,
  findPackage,
  findPackageByLabel,
  formatColones,
  PACKAGE_LABELS,
  packageLabel,
  priceAfterPackageChange,
  quantityOf,
  withQuantity,
} from '../pricing';

/** An extra with a price, to test the arithmetic; the configured extras may still have none. */
const MAKEUP = { code: 'makeup', label: 'Maquillaje', unitPrice: 20000 };

describe('the configuration', () => {
  it('prices the three packages and leaves Custom and Otro for the photographer to set', () => {
    expect(PACKAGES.map((option) => [option.label, option.price, option.durationHours])).toEqual([
      ['Básico', 35000, 1],
      ['Estándar', 65000, 2],
      ['Premium', 110000, 3],
      ['Custom', null, null],
      ['Otro', null, null],
    ]);
  });

  it('offers the six extras, each with its own code', () => {
    expect(EXTRAS.map((extra) => extra.label)).toEqual([
      'Hora adicional',
      'Persona adicional',
      'Foto editada adicional',
      'Entrega urgente',
      'Impresión adicional',
      'Maquillaje',
    ]);
    expect(new Set(EXTRAS.map((extra) => extra.code)).size).toBe(EXTRAS.length);
  });
});

describe('packages', () => {
  it('are found by the name in the drop-down or by their tier', () => {
    expect(findPackageByLabel('Estándar')?.tier).toBe('Standard');
    expect(findPackageByLabel('Nada')).toBeUndefined();
    expect(findPackage('Premium')?.label).toBe('Premium');
    expect(PACKAGE_LABELS).toEqual(['Básico', 'Estándar', 'Premium', 'Custom', 'Otro']);
  });

  it('show an empty name while none is chosen', () => {
    expect(packageLabel(null)).toBe('');
    expect(packageLabel('Basic')).toBe('Básico');
  });
});

describe('priceAfterPackageChange', () => {
  const standard = PACKAGES[1]!;
  const custom = PACKAGES[3]!;

  it('fills in the price of a package that has one, even over what was typed', () => {
    expect(priceAfterPackageChange('90000', null, standard)).toBe('65000');
  });

  it('clears the price the previous package filled in when the next one has none', () => {
    expect(priceAfterPackageChange('65000', 'Standard', custom)).toBe('');
    expect(priceAfterPackageChange('65.000', 'Standard', custom)).toBe('');
  });

  it('keeps what the photographer typed when the next package has no price', () => {
    expect(priceAfterPackageChange('70000', 'Standard', custom)).toBe('70000');
    expect(priceAfterPackageChange('70000', null, custom)).toBe('70000');
    expect(priceAfterPackageChange('70000', 'Other', custom)).toBe('70000');
  });
});

describe('the selection of extras', () => {
  it('starts empty and counts units by code', () => {
    expect(quantityOf([], 'makeup')).toBe(0);
    expect(quantityOf([{ code: 'makeup', name: 'Maquillaje', unitPrice: 20000, quantity: 3 }], 'makeup')).toBe(3);
  });

  it('adds an extra with the name and the price it has now', () => {
    const selection = withQuantity([], MAKEUP, 2);

    expect(selection).toEqual([{ code: 'makeup', name: 'Maquillaje', unitPrice: 20000, quantity: 2 }]);
  });

  it('removes an extra when its quantity goes back to zero', () => {
    const selection = withQuantity(withQuantity([], MAKEUP, 1), MAKEUP, 0);

    expect(selection).toEqual([]);
  });

  it('never goes below zero or above the maximum', () => {
    expect(withQuantity([], MAKEUP, -3)).toEqual([]);
    expect(quantityOf(withQuantity([], MAKEUP, 500), 'makeup')).toBe(MAX_EXTRA_QUANTITY);
  });

  it('keeps the extras in the order of the configuration', () => {
    const first = EXTRAS[0]!;
    const last = EXTRAS[EXTRAS.length - 1]!;

    const selection = withQuantity(withQuantity([], last, 1), first, 1);

    expect(selection.map((extra) => extra.code)).toEqual([first.code, last.code]);
  });

  it('adds up every unit of every extra', () => {
    const selection: readonly SelectedExtra[] = [
      { code: 'a', name: 'A', unitPrice: 15000, quantity: 2 },
      { code: 'b', name: 'B', unitPrice: 20000, quantity: 1 },
    ];

    expect(extrasTotal(selection)).toBe(50000);
    expect(extrasTotal([])).toBe(0);
  });
});

describe('computeTotals', () => {
  const extras: readonly SelectedExtra[] = [{ code: 'makeup', name: 'Maquillaje', unitPrice: 20000, quantity: 1 }];

  it('adds the extras to the package and subtracts the discount', () => {
    expect(computeTotals('65 000', extras, '5000')).toEqual({ packagePrice: 65000, extrasTotal: 20000, discount: 5000, total: 80000 });
  });

  it('counts an empty or invalid field as zero instead of failing while the photographer types', () => {
    expect(computeTotals('', [], '')).toEqual({ packagePrice: 0, extrasTotal: 0, discount: 0, total: 0 });
    expect(computeTotals('abc', extras, 'xyz').total).toBe(20000);
  });

  it('never shows a negative total', () => {
    expect(computeTotals('1000', [], '5000').total).toBe(0);
  });
});

describe('formatColones', () => {
  it('writes an amount in colones', () => {
    expect(formatColones(35000)).toContain('35');
    expect(formatColones(35000)).toContain('₡');
  });
});
