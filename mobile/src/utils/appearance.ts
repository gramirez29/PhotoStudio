import { THEME_PREFERENCES } from '../constants/appearance';
import type { ColorScheme, ThemePreference } from '../types/theme/palette.types';

/**
 * Checks that a value read from the storage is one of the choices the app offers.
 * @param value Value to check.
 * @returns True when it is a theme preference.
 */
export function isThemePreference(value: unknown): value is ThemePreference {
  return typeof value === 'string' && (THEME_PREFERENCES as readonly string[]).includes(value);
}

/**
 * Works out the color scheme the app shows: what the photographer chose or, when they chose to follow the phone, what the
 * phone says. A phone that says nothing counts as light.
 * @param preference What the photographer chose.
 * @param system What the phone says (`light`, `dark`, or nothing or "unspecified").
 * @returns The scheme to show.
 */
export function resolveColorScheme(preference: ThemePreference, system: string | null | undefined): ColorScheme {
  if (preference === 'system') {
    return system === 'dark' ? 'dark' : 'light';
  }

  return preference;
}

/**
 * Names a theme preference for the selector.
 * @param preference Choice to name.
 * @returns Its label in Spanish.
 */
export function themePreferenceLabel(preference: ThemePreference): string {
  switch (preference) {
    case 'system':
      return 'Sistema';
    case 'light':
      return 'Claro';
    case 'dark':
      return 'Oscuro';
  }
}
