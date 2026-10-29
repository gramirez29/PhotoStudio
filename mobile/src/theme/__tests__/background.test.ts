import type { ColorScheme } from '../../types/theme/palette.types';
import { WASH_OPACITY } from '../background';
import { getPalette } from '../palettes';

/**
 * Mixes the wash over a gray of the picture, as the screen does.
 * @param washHex Color of the wash, #rrggbb.
 * @param opacity How much of the wash covers the picture, from 0 to 1.
 * @param pictureLevel Gray of the picture under the wash, from 0 (black) to 255 (white).
 * @returns The color seen on the screen, #rrggbb.
 */
function seenColor(washHex: string, opacity: number, pictureLevel: number): string {
  const channels = [1, 3, 5].map((offset) => {
    const wash = parseInt(washHex.slice(offset, offset + 2), 16);
    return Math.round(wash * opacity + pictureLevel * (1 - opacity));
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

describe.each<ColorScheme>(['light', 'dark'])('the picture behind the app, in the %s theme', (scheme) => {
  const palette = getPalette(scheme);
  const opacity = WASH_OPACITY[scheme];
  // The picture goes from black to white; the text must read over the worst of those two ends.
  const ends = [seenColor(palette.wash, opacity, 0), seenColor(palette.wash, opacity, 255)];

  it('is a soft wash: the picture shows through, but it never hides the screen', () => {
    expect(opacity).toBeGreaterThan(0.6);
    expect(opacity).toBeLessThan(0.9);
  });

  it.each(ends)('keeps the text readable on %s, whatever part of the picture is under it', (background) => {
    expect(contrast(palette.onBackground, background)).toBeGreaterThanOrEqual(4.5);
    expect(contrast(palette.onSurfaceVariant, background)).toBeGreaterThanOrEqual(4.5);
    expect(contrast(palette.muted, background)).toBeGreaterThanOrEqual(4.5);
  });

  it('lets the picture be seen: the darkest and the lightest part differ by at least 10 levels of luminance', () => {
    const [darkest = '#000000', lightest = '#000000'] = ends;
    const levels = (hex: string): number => Math.round(luminance(hex) * 255);

    expect(Math.abs(levels(lightest) - levels(darkest))).toBeGreaterThanOrEqual(10);
  });
});
