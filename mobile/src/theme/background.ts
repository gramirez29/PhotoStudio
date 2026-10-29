import type { ColorScheme } from '../types/theme/palette.types';

/**
 * How much of the wash covers the picture behind the app, by scheme: 1 hides it, 0 shows it. The picture is already blurred and
 * stretched to the full range from dark to light (see scripts/blur-background.ps1); the wash, white in the light theme and navy
 * in the dark one, takes it to a soft texture that keeps the text readable. The test of this file keeps both: the picture can
 * be seen, and the text reads over its darkest and lightest parts. Lower a value to see more of the picture.
 */
export const WASH_OPACITY: Readonly<Record<ColorScheme, number>> = {
  light: 0.78,
  dark: 0.78,
};
