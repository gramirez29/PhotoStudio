import type { ColorScheme } from '../types/theme/palette.types';

/**
 * How visible the logo behind the app is, by scheme: 0 hides it, 1 paints it with the full color. It is a watermark: the
 * shape of the logo has to be there to be found, but it must never compete with what is on the screen. The test of this file
 * keeps the text readable over the color the watermark makes at its strongest.
 */
export const WATERMARK_OPACITY: Readonly<Record<ColorScheme, number>> = {
  light: 0.07,
  dark: 0.1,
};

/** How big the watermark is, as a share of the narrower side of the window; above 1 it bleeds off the edges. */
export const WATERMARK_SCALE = 1.1;
