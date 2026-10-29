import { configureFonts, MD3DarkTheme, MD3LightTheme, type MD3Theme } from 'react-native-paper';
import type { ColorScheme, Palette } from '../types/theme/palette.types';
import { withAlpha } from '../utils/color';
import { FONT_FAMILIES } from './fonts';
import { getPalette } from './palettes';

/** Tints of the surfaces that sit higher than the screen (cards, menus, dialogs), by scheme. */
const ELEVATION = {
  light: { level0: 'transparent', level1: '#f9e6d8', level2: '#f8dfcd', level3: '#f7dac5', level4: '#f6d8c2', level5: '#f6d5be' },
  dark: { level0: 'transparent', level1: '#071433', level2: '#0c1a3b', level3: '#112043', level4: '#16284a', level5: '#1b2c4e' },
} as const;

/** Font family and weight of every style of the type scale of Material Design 3. Titles use Josefin Sans, the rest DM Sans. */
const TYPE_SCALE = {
  displayLarge: { fontFamily: FONT_FAMILIES.title },
  displayMedium: { fontFamily: FONT_FAMILIES.title },
  displaySmall: { fontFamily: FONT_FAMILIES.title },
  headlineLarge: { fontFamily: FONT_FAMILIES.title },
  headlineMedium: { fontFamily: FONT_FAMILIES.title },
  headlineSmall: { fontFamily: FONT_FAMILIES.title },
  titleLarge: { fontFamily: FONT_FAMILIES.title },
  titleMedium: { fontFamily: FONT_FAMILIES.title },
  titleSmall: { fontFamily: FONT_FAMILIES.bodyMedium },
  labelLarge: { fontFamily: FONT_FAMILIES.bodyMedium },
  labelMedium: { fontFamily: FONT_FAMILIES.bodyMedium },
  labelSmall: { fontFamily: FONT_FAMILIES.bodyMedium },
  bodyLarge: { fontFamily: FONT_FAMILIES.body },
  bodyMedium: { fontFamily: FONT_FAMILIES.body },
  bodySmall: { fontFamily: FONT_FAMILIES.body },
} as const;

/**
 * Turns a palette into the colors of the component library. The library asks for more roles than the palette names; the ones
 * the palette does not name keep the values of the Material Design 3 base theme of the scheme.
 * @param base Base theme of the scheme.
 * @param palette Palette to apply.
 * @param scheme Color scheme.
 * @returns The colors of the theme.
 */
function mapColors(base: MD3Theme, palette: Palette, scheme: ColorScheme): MD3Theme['colors'] {
  return {
    ...base.colors,
    primary: palette.primary,
    onPrimary: palette.onPrimary,
    primaryContainer: palette.primaryContainer,
    onPrimaryContainer: palette.onPrimaryContainer,
    secondary: palette.secondary,
    onSecondary: palette.onSecondary,
    secondaryContainer: palette.secondaryContainer,
    onSecondaryContainer: palette.onSecondaryContainer,
    background: palette.background,
    onBackground: palette.onBackground,
    surface: palette.surface,
    onSurface: palette.onSurface,
    surfaceVariant: palette.surfaceVariant,
    onSurfaceVariant: palette.onSurfaceVariant,
    outline: palette.outline,
    outlineVariant: palette.outlineVariant,
    error: palette.error,
    onError: palette.onError,
    errorContainer: palette.errorContainer,
    onErrorContainer: palette.onErrorContainer,
    backdrop: withAlpha(palette.scrim, 0.45),
    shadow: '#000000',
    scrim: palette.scrim,
    elevation: ELEVATION[scheme],
  };
}

/**
 * Builds the theme of the component library (Material Design 3) for a color scheme, with the colors of the brand and its two
 * fonts. There is no dynamic color: the phone's wallpaper never changes the palette.
 * @param scheme Color scheme.
 * @returns The theme.
 */
export function buildPaperTheme(scheme: ColorScheme): MD3Theme {
  const base = scheme === 'dark' ? MD3DarkTheme : MD3LightTheme;
  const fonts = configureFonts({
    config: Object.fromEntries(Object.entries(TYPE_SCALE).map(([variant, style]) => [variant, { ...style, fontWeight: '400' as const }])),
  });

  return {
    ...base,
    dark: scheme === 'dark',
    colors: mapColors(base, getPalette(scheme), scheme),
    // A text without a variant uses the default font, which is DM Sans like the running text.
    fonts: { ...fonts, default: { ...fonts.default, fontFamily: FONT_FAMILIES.body } },
  };
}
