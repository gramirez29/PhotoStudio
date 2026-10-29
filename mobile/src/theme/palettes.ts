import type { ColorScheme, Palette } from '../types/theme/palette.types';

/**
 * The light theme of the brand: cream and terracotta, with the slate blue of the logo. It is the same palette as the web portal
 * (portal/src/theme/tokens.css), except for the main action color: the portal paints a button with #C4552E and a bold 19px
 * label, which is large text (3:1). The app has smaller labels, so its main color is the darker #B04A25 (4.96:1 with cream).
 */
export const LIGHT_PALETTE: Palette = {
  primary: '#b04a25',
  onPrimary: '#fbf3ea',
  primaryContainer: '#f6d5be',
  onPrimaryContainer: '#4a1a07',
  secondary: '#3a4466',
  onSecondary: '#ffffff',
  secondaryContainer: '#dadde8',
  onSecondaryContainer: '#0b1a40',
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
