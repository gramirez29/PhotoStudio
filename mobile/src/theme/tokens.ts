/**
 * The scales of the interface that are not colors: the spacing and the corner radius. The colors are not here: they belong to
 * the theme (`useAppTheme`, `makeStyles`, `src/theme/palettes.ts`), so they follow the light and the dark themes.
 */

/** Spacing scale in density-independent pixels. */
export const spacing = {
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
} as const;

/** Corner radius scale. */
export const radius = {
  sm: 6,
  md: 12,
  pill: 999,
} as const;
