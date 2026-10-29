import type { ColorScheme, Palette } from './palette.types';

/** Writes the styles of a component from the colors of the theme: it runs again whenever the theme changes. */
export type StylesFactory<T> = (palette: Palette, scheme: ColorScheme) => T;
