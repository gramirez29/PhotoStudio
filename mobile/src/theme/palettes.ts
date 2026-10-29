import type { ColorScheme, Palette } from '../types/theme/palette.types';

/**
 * The light theme of the brand: cream, the slate blue of the logo and terracotta. The main color of the app, the one of its
 * buttons, its focus and its selected states, is the slate blue of the logo (#3A4466, 9.5:1 with white): it is the color of the
 * header band, it never gets confused with the red of the destructive actions and it reads well on the peach cards. The
 * terracotta is the second accent. (The web portal paints its buttons terracotta, #C4552E with a bold 19px label, which is
 * large text; the app has smaller labels and a different job for each color.)
 */
export const LIGHT_PALETTE: Palette = {
  primary: '#3a4466',
  onPrimary: '#ffffff',
  primaryContainer: '#dadde8',
  onPrimaryContainer: '#0b1a40',
  secondary: '#b04a25',
  onSecondary: '#fbf3ea',
  secondaryContainer: '#f6d5be',
  onSecondaryContainer: '#4a1a07',
  background: '#fbf3ea',
  onBackground: '#0b1a40',
  surface: '#fbf3ea',
  onSurface: '#0b1a40',
  surfaceVariant: '#f6d5be',
  onSurfaceVariant: '#3a4466',
  outline: '#7c6f68',
  outlineVariant: '#dcc3ae',
  error: '#b3261e',
  onError: '#ffffff',
  errorContainer: '#f9dedc',
  onErrorContainer: '#410e0b',
  success: '#23683f',
  warning: '#8a5210',
  info: '#3a4466',
  muted: '#4d4959',
  scrim: '#0b1a40',
  wash: '#ffffff',
  brand: '#3a4466',
  onBrand: '#ffffff',
  silver: '#c9cdd6',
};

/** The dark theme of the brand: navy and copper, the colors of the logo. The same palette as the dark theme of the web portal. */
export const DARK_PALETTE: Palette = {
  primary: '#e3a97f',
  onPrimary: '#020d2c',
  primaryContainer: '#4a2e1a',
  onPrimaryContainer: '#f6d5be',
  secondary: '#c9cdd6',
  onSecondary: '#0b1a40',
  secondaryContainer: '#3a4466',
  onSecondaryContainer: '#f4f1ec',
  background: '#020d2c',
  onBackground: '#f4f1ec',
  surface: '#020d2c',
  onSurface: '#f4f1ec',
  surfaceVariant: '#26324f',
  onSurfaceVariant: '#c9cdd6',
  outline: '#8a93a8',
  outlineVariant: '#3a4466',
  error: '#f2766b',
  onError: '#410e0b',
  errorContainer: '#8c1d18',
  onErrorContainer: '#f9dedc',
  success: '#6fcf97',
  warning: '#e3a97f',
  info: '#a9b7e8',
  muted: '#b8c0d4',
  scrim: '#000000',
  wash: '#020d2c',
  brand: '#3a4466',
  onBrand: '#ffffff',
  silver: '#c9cdd6',
};

/**
 * Gives the palette of a color scheme.
 * @param scheme Color scheme.
 * @returns Its palette.
 */
export function getPalette(scheme: ColorScheme): Palette {
  return scheme === 'dark' ? DARK_PALETTE : LIGHT_PALETTE;
}
