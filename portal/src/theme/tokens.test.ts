import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

/** The stylesheet that defines the tokens, read as text: it is the single source of the palette. */
const css = readFileSync(resolve(__dirname, 'tokens.css'), 'utf8');

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
];

describe.each(Object.entries(variations))('%s palette', (_name, tokens) => {
  it.each(TEXT_PAIRS)('keeps %s readable on %s (WCAG AA, 4.5:1)', (foreground, background) => {
    const front = tokens[foreground];
    const back = tokens[background];

    expect(front).toBeDefined();
    expect(back).toBeDefined();
    expect(contrast(front ?? '', back ?? '')).toBeGreaterThanOrEqual(4.5);
  });

  // The light terracotta (#C4552E) is 4.08:1 against its cream text, under the 4.5:1 of normal text. It is only used where
  // WCAG asks for 3:1: the label of a button, which is bold and 19px (large text), and fills and borders (non-text). It is
  // never used as the color of running text. If a pair below must hold 4.5:1, the brand accent has to be darkened.
  it('keeps the label of an accent button readable as large text (3:1)', () => {
    expect(contrast(tokens['--color-on-accent'] ?? '', tokens['--color-accent'] ?? '')).toBeGreaterThanOrEqual(3);
  });

  it('keeps the accent visible against the background as a fill or a border (3:1)', () => {
    expect(contrast(tokens['--color-accent'] ?? '', tokens['--color-bg'] ?? '')).toBeGreaterThanOrEqual(3);
  });

  it.each(['--color-success', '--color-warning', '--color-danger'])('keeps the %s dot visible on the surface (3:1)', (dot) => {
    expect(contrast(tokens[dot] ?? '', tokens['--color-surface'] ?? '')).toBeGreaterThanOrEqual(3);
  });
});

describe('brand palette', () => {
  it('uses exactly the colors the brand defined for each variation', () => {
    const lightBrand = ['#fbf3ea', '#f6d5be', '#0b1a40', '#3a4466', '#c4552e'];
    const darkBrand = ['#020d2c', '#26324f', '#e3a97f', '#c9cdd6', '#f4f1ec'];

    expect(lightBrand.every((color) => Object.values(variations.light).includes(color))).toBe(true);
    expect(darkBrand.every((color) => Object.values(variations.dark).includes(color))).toBe(true);
  });
});
