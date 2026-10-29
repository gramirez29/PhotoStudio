import { LIGHT_PALETTE } from './palettes';

/**
 * Colors of the light theme under the names the app used before the theme existed. It is a compatibility layer, not the way to
 * read a color: a color read here never changes with the theme. Components that read it keep working, always in light, until
 * they move to the theme of the app (`useAppTheme`), one by one, in the phases of docs/MobileUi.md; then this file goes away.
 * The spacing and the radius below are not colors and stay.
 */
export const colors = {
  background: LIGHT_PALETTE.background,
  surface: LIGHT_PALETTE.surfaceVariant,
  border: LIGHT_PALETTE.outlineVariant,
  textPrimary: LIGHT_PALETTE.onBackground,
  textSecondary: LIGHT_PALETTE.onSurfaceVariant,
  textInverse: LIGHT_PALETTE.onPrimary,
  primary: LIGHT_PALETTE.primary,
  success: LIGHT_PALETTE.success,
  warning: LIGHT_PALETTE.warning,
  danger: LIGHT_PALETTE.error,
  info: LIGHT_PALETTE.info,
  muted: LIGHT_PALETTE.muted,
  overlay: 'rgba(11, 26, 64, 0.45)',
} as const;

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
