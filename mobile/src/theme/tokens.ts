/**
 * Design tokens. The values are a temporary neutral palette until the brand palette is defined;
 * components read colors only from here, so replacing the palette does not touch any screen.
 */
export const colors = {
  background: '#FFFFFF',
  surface: '#F4F4F5',
  border: '#E4E4E7',
  textPrimary: '#18181B',
  textSecondary: '#52525B',
  textInverse: '#FFFFFF',
  primary: '#27272A',
  success: '#15803D',
  warning: '#B45309',
  danger: '#B91C1C',
  info: '#1D4ED8',
  muted: '#71717A',
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

/** Name of a color token. */
export type ColorToken = keyof typeof colors;
