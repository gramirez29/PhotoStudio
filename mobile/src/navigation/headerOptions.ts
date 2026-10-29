import { FONT_FAMILIES } from '../theme/fonts';
import type { HeaderOptions } from '../types/navigation/headerOptions.types';
import type { Palette } from '../types/theme/palette.types';

/**
 * The header of the app, the same band the web has: the slate blue of the logo with the title in white. It is the same in the
 * light and in the dark theme, because the brand color is.
 * @param palette Palette of the theme being shown.
 * @returns The options every screen of the stack shares.
 */
export function buildHeaderOptions(palette: Palette): HeaderOptions {
  return {
    headerTitleAlign: 'center',
    headerStyle: { backgroundColor: palette.brand },
    headerTintColor: palette.onBrand,
    headerTitleStyle: { fontFamily: FONT_FAMILIES.title, fontSize: 18, color: palette.onBrand },
    headerShadowVisible: false,
  };
}
