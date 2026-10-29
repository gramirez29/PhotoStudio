import type { ColorScheme } from '../types/theme/palette.types';

/**
 * How much of the wash covers the picture behind the app, by scheme: 1 hides it, 0 shows it. The picture is already blurred
 * (see scripts/blur-background.ps1); the wash, white in the light theme and navy in the dark one, takes it to a soft texture
 * that keeps the text readable.
 */
export const WASH_OPACITY: Readonly<Record<ColorScheme, number>> = {
  light: 0.93,
  dark: 0.92,
};
