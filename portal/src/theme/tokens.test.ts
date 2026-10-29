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

const variations = {
  light: readBlock(':root {'),
  dark: readBlock('@media (prefers-color-scheme: dark) {\n  :root {'),
} as const;

/** Pairs used for text on screen, with the minimum ratio WCAG AA asks for (4.5 for text). */
const TEXT_PAIRS: readonly (readonly [string, string])[] = [
  ['--color-text', '--color-bg'],
  ['--color-text', '--color-surface'],
  ['--color-text-muted', '--color-bg'],
  ['--color-text-muted', '--color-surface'],
  // The headings, on the page and inside the cards.
  ['--color-heading', '--color-bg'],
  ['--color-heading', '--color-surface'],
  // The name of the studio on the band of the logo.
  ['--color-on-brand', '--color-brand'],
  // The label of an action.
  ['--color-on-accent', '--color-accent'],
];

describe.each(Object.entries(variations))('%s palette', (_name, tokens) => {
  it.each(TEXT_PAIRS)('keeps %s readable on %s (WCAG AA, 4.5:1)', (foreground, background) => {
    // A variation only declares what it changes: the rest is inherited from the light one.
    const front = tokens[foreground] ?? variations.light[foreground];
    const back = tokens[background] ?? variations.light[background];

    expect(front).toBeDefined();
    expect(back).toBeDefined();
    expect(contrast(front ?? '', back ?? '')).toBeGreaterThanOrEqual(4.5);
  });

  it('keeps the focus ring, which is the heading color, visible against the page (3:1)', () => {
    const ring = tokens['--color-heading'] ?? variations.light['--color-heading'];

    expect(contrast(ring ?? '', tokens['--color-bg'] ?? variations.light['--color-bg'] ?? '')).toBeGreaterThanOrEqual(3);
  });

  it('keeps the silver border of the logo visible against the band (3:1)', () => {
    const silver = tokens['--color-silver'] ?? variations.light['--color-silver'];
    const band = tokens['--color-brand'] ?? variations.light['--color-brand'];

    expect(contrast(silver ?? '', band ?? '')).toBeGreaterThanOrEqual(3);
  });

  it.each(['--color-success', '--color-warning', '--color-danger'])('keeps the %s dot visible on the surface (3:1)', (dot) => {
    const color = tokens[dot] ?? variations.light[dot];
    const surface = tokens['--color-surface'] ?? variations.light['--color-surface'];

    expect(contrast(color ?? '', surface ?? '')).toBeGreaterThanOrEqual(3);
  });
});

describe('brand palette', () => {
  it('light is the palette of the mobile app (its tokens.ts) plus the brand color of the logo', () => {
    const appPalette = ['#ffffff', '#f4f4f5', '#18181b', '#52525b', '#27272a', '#15803d', '#b45309', '#b91c1c'];

    expect(appPalette.every((color) => Object.values(variations.light).includes(color))).toBe(true);
    expect(variations.light['--color-brand']).toBe('#3a4466');
  });

  it('dark is the navy and copper palette the web had before, which the app will use for its own dark mode', () => {
    const darkBrand = ['#020d2c', '#26324f', '#e3a97f', '#c9cdd6', '#f4f1ec'];

    expect(darkBrand.every((color) => Object.values(variations.dark).includes(color))).toBe(true);
  });

  it('keeps the brand color on the band in both variations', () => {
    expect(variations.dark['--color-brand']).toBeUndefined();
    expect(variations.light['--color-brand']).toBe('#3a4466');
  });
});
