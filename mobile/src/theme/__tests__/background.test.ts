import type { ColorScheme } from '../../types/theme/palette.types';
import { WATERMARK_OPACITY } from '../background';
import { getPalette } from '../palettes';

/**
 * Mixes the color of the logo over the color of the background, as the screen does where the logo is solid.
 * @param tintHex Color of the logo, #rrggbb.
 * @param opacity How much of the logo color shows, from 0 to 1.
 * @param backgroundHex Color of the background under it, #rrggbb.
 * @returns The color seen on the screen, #rrggbb.
 */
function seenColor(tintHex: string, opacity: number, backgroundHex: string): string {
  const channels = [1, 3, 5].map((offset) => {
    const tint = parseInt(tintHex.slice(offset, offset + 2), 16);
    const background = parseInt(backgroundHex.slice(offset, offset + 2), 16);
    return Math.round(tint * opacity + background * (1 - opacity));
  });
  return `#${channels.map((value) => value.toString(16).padStart(2, '0')).join('')}`;
}

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

describe.each<ColorScheme>(['light', 'dark'])('the logo behind the app, in the %s theme', (scheme) => {
  const palette = getPalette(scheme);
  const opacity = WATERMARK_OPACITY[scheme];
  // Where the logo is solid, the screen shows the background mixed with the logo color: the worst place for the text.
  const strongest = seenColor(palette.primary, opacity, palette.background);

  it('is a faint watermark: it can be seen, and it never competes with the screen', () => {
    expect(opacity).toBeGreaterThanOrEqual(0.04);
    expect(opacity).toBeLessThanOrEqual(0.12);
  });

  it('keeps the text readable on the background, and on the logo where it is strongest', () => {
    for (const background of [palette.background, strongest]) {
      expect(contrast(palette.onBackground, background)).toBeGreaterThanOrEqual(4.5);
      expect(contrast(palette.onSurfaceVariant, background)).toBeGreaterThanOrEqual(4.5);
      expect(contrast(palette.muted, background)).toBeGreaterThanOrEqual(4.5);
    }
  });

  it('lets the logo be seen: it differs from the background by at least 5 % of contrast', () => {
    expect(contrast(strongest, palette.background)).toBeGreaterThanOrEqual(1.05);
  });
});
