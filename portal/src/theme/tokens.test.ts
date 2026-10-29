import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

/** The stylesheet that defines the tokens, read as text: it is the single source of the palette. */
const css = readFileSync(resolve(__dirname, 'tokens.css'), 'utf8').replace(/\r\n/g, '\n');

/**
 * Reads the variables declared inside the first block that follows a marker.
 * @param marker Text that opens the block, for example `:root {`.
 * @returns The variables of that block, by name.
 */
function readBlock(marker: string): Readonly<Record<string, string>> {
  const start = css.indexOf(marker);
  const end = css.indexOf('}', start);
  const variables: Record<string, string> = {};
  for (const match of css.slice(start, end).matchAll(/(--[\w-]+):\s*(#[0-9a-fA-F]{6})\s*;/g)) {
    const [, name, value] = match;
    if (name !== undefined && value !== undefined) {
      variables[name] = value.toLowerCase();
    }
  }

  return variables;
}

/**
 * Computes the relative luminance of a color, as WCAG defines it.
 * @param hex Color as #rrggbb.
 * @returns The luminance, from 0 (black) to 1 (white).
 */
function luminance(hex: string): number {
  const channels = [1, 3, 5].map((offset) => parseInt(hex.slice(offset, offset + 2), 16) / 255);
  const [red = 0, green = 0, blue = 0] = channels.map((value) => (value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4));
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
}

/**
 * Computes the contrast ratio between two colors, as WCAG defines it.
 * @param first First color as #rrggbb.
 * @param second Second color as #rrggbb.
 * @returns The ratio, from 1 (none) to 21 (black on white).
 */
function contrast(first: string, second: string): number {
  const [light, dark] = [luminance(first), luminance(second)].sort((a, b) => b - a);
  return ((light ?? 0) + 0.05) / ((dark ?? 0) + 0.05);
}

/** The palette: the only variation, as in the app. */
const palette = readBlock(':root {');

/** Pairs used for text on screen, with the minimum ratio WCAG AA asks for (4.5 for text). */
const TEXT_PAIRS: readonly (readonly [string, string])[] = [
  ['--color-text', '--color-bg'],
  ['--color-text', '--color-surface'],
  ['--color-text-muted', '--color-bg'],
  ['--color-text-muted', '--color-surface'],
  // The headings are written in the brand color, on the page and inside the cards.
  ['--color-brand', '--color-bg'],
  ['--color-brand', '--color-surface'],
  // The name of the studio on the band of the logo.
  ['--color-on-brand', '--color-brand'],
  // The label of an action.
  ['--color-on-accent', '--color-accent'],
];

describe('palette', () => {
  it.each(TEXT_PAIRS)('keeps %s readable on %s (WCAG AA, 4.5:1)', (foreground, background) => {
    const front = palette[foreground];
    const back = palette[background];

    expect(front).toBeDefined();
    expect(back).toBeDefined();
    expect(contrast(front ?? '', back ?? '')).toBeGreaterThanOrEqual(4.5);
  });

  it('keeps the focus ring, which is the brand color, visible against the page (3:1)', () => {
    expect(contrast(palette['--color-brand'] ?? '', palette['--color-bg'] ?? '')).toBeGreaterThanOrEqual(3);
  });

  it.each(['--color-success', '--color-warning', '--color-danger'])('keeps the %s dot visible on the surface (3:1)', (dot) => {
    expect(contrast(palette[dot] ?? '', palette['--color-surface'] ?? '')).toBeGreaterThanOrEqual(3);
  });
});

describe('brand palette', () => {
  it('is the palette of the mobile app (src/theme/tokens.ts of the app) plus the brand color of the logo', () => {
    const appPalette = ['#ffffff', '#f4f4f5', '#18181b', '#52525b', '#27272a', '#15803d', '#b45309', '#b91c1c'];

    expect(appPalette.every((color) => Object.values(palette).includes(color))).toBe(true);
    expect(palette['--color-brand']).toBe('#3a4466');
  });

  it('has no dark variation: it is the same on every device, as the app is', () => {
    expect(css).not.toContain('prefers-color-scheme');
  });
});
