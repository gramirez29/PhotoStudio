import type { ColorScheme, Palette } from '../../types/theme/palette.types';
import { DARK_PALETTE, getPalette, LIGHT_PALETTE } from '../palettes';

/**
 * Computes the relative luminance of a color, as WCAG defines it.
 * @param hex Color as #rrggbb.
 * @returns The luminance, from 0 (black) to 1 (white).
 */
function luminance(hex: string): number {
  const [red = 0, green = 0, blue = 0] = [1, 3, 5]
    .map((offset) => parseInt(hex.slice(offset, offset + 2), 16) / 255)
    .map((value) => (value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4));
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
}

/**
 * Computes the contrast ratio between two colors, as WCAG defines it.
 * @param first First color as #rrggbb.
 * @param second Second color as #rrggbb.
 * @returns The ratio, from 1 (none) to 21.
 */
function contrast(first: string, second: string): number {
  const [light = 0, dark = 0] = [luminance(first), luminance(second)].sort((a, b) => b - a);
  return (light + 0.05) / (dark + 0.05);
}

const SCHEMES: readonly ColorScheme[] = ['light', 'dark'];

/** Pairs of roles that carry text or icons, with the ratio WCAG AA asks for (4.5:1). */
const TEXT_PAIRS: readonly (readonly [keyof Palette, keyof Palette])[] = [
  ['onBackground', 'background'],
  ['onSurface', 'surface'],
  ['onSurfaceVariant', 'surfaceVariant'],
  ['onSurfaceVariant', 'background'],
  ['onPrimary', 'primary'],
  ['onPrimaryContainer', 'primaryContainer'],
  ['onSecondary', 'secondary'],
  ['onSecondaryContainer', 'secondaryContainer'],
  ['onError', 'error'],
  ['onErrorContainer', 'errorContainer'],
  ['onBrand', 'brand'],
  // Text that is the color of a state, on the screen and on a card.
  ['primary', 'background'],
  ['error', 'background'],
  ['error', 'surfaceVariant'],
  ['success', 'background'],
  ['success', 'surfaceVariant'],
  ['warning', 'background'],
  ['warning', 'surfaceVariant'],
  ['info', 'background'],
  ['info', 'surfaceVariant'],
  ['muted', 'background'],
  ['muted', 'surfaceVariant'],
  // The name of a status, written over the fill of its color.
  ['onError', 'success'],
  ['onError', 'warning'],
  ['onError', 'info'],
  ['onError', 'muted'],
  // The option chosen in a list, and the floating button, in the main color of the app.
  ['onPrimary', 'primary'],
  ['onBrand', 'brand'],
];

describe.each(SCHEMES)('%s palette', (scheme) => {
  const palette = getPalette(scheme);

  it.each(TEXT_PAIRS)('keeps %s readable on %s (WCAG AA, 4.5:1)', (foreground, background) => {
    expect(contrast(palette[foreground], palette[background])).toBeGreaterThanOrEqual(4.5);
  });

  it('keeps the border of a field visible against the screen (3:1)', () => {
    expect(contrast(palette.outline, palette.background)).toBeGreaterThanOrEqual(3);
  });

  it('keeps the silver of the logo visible against the band of the brand (3:1)', () => {
    expect(contrast(palette.silver, palette.brand)).toBeGreaterThanOrEqual(3);
  });

  it('writes every color as #rrggbb', () => {
    expect(Object.values(palette).every((color) => /^#[0-9a-f]{6}$/.test(color))).toBe(true);
  });
});

describe('the brand', () => {
  it('is the cream, slate blue and terracotta palette of the web in light, with the slate blue of the logo as the main color', () => {
    expect([LIGHT_PALETTE.background, LIGHT_PALETTE.surfaceVariant, LIGHT_PALETTE.onBackground, LIGHT_PALETTE.brand]).toEqual([
      '#fbf3ea',
      '#f6d5be',
      '#0b1a40',
      '#3a4466',
    ]);
    expect(LIGHT_PALETTE.primary).toBe('#3a4466');
    expect(LIGHT_PALETTE.secondary).toBe('#b04a25');
  });

  it('is the navy and copper palette of the web in dark', () => {
    expect([DARK_PALETTE.background, DARK_PALETTE.surfaceVariant, DARK_PALETTE.primary, DARK_PALETTE.onSurfaceVariant, DARK_PALETTE.onBackground]).toEqual([
      '#020d2c',
      '#26324f',
      '#e3a97f',
      '#c9cdd6',
      '#f4f1ec',
    ]);
  });

  it('keeps the slate blue of the logo on the band in both themes', () => {
    expect(LIGHT_PALETTE.brand).toBe('#3a4466');
    expect(DARK_PALETTE.brand).toBe('#3a4466');
  });

  it('gives the palette of each scheme', () => {
    expect(getPalette('light')).toBe(LIGHT_PALETTE);
    expect(getPalette('dark')).toBe(DARK_PALETTE);
  });
});
