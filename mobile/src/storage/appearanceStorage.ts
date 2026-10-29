import * as SecureStore from 'expo-secure-store';
import type { ThemePreference } from '../types/theme/palette.types';
import { isThemePreference } from '../utils/appearance';

/** Key under which the choice of theme is kept on the device. */
export const THEME_PREFERENCE_KEY = 'photostudio.themePreference';

/**
 * Saves the choice of theme. A failure is swallowed: the choice keeps working until the app closes and the photographer only
 * has to make it again.
 * @param preference Choice to keep.
 * @returns A promise that resolves when the attempt finished.
 */
export async function saveThemePreference(preference: ThemePreference): Promise<void> {
  try {
    await SecureStore.setItemAsync(THEME_PREFERENCE_KEY, preference);
  } catch {
    // The storage can be unavailable; the choice stays in memory.
  }
}

/**
 * Reads the saved choice of theme.
 * @returns The choice, or null when none is saved, it is not a valid one or the storage cannot be read.
 */
export async function loadThemePreference(): Promise<ThemePreference | null> {
  try {
    const saved = await SecureStore.getItemAsync(THEME_PREFERENCE_KEY);
    return isThemePreference(saved) ? saved : null;
  } catch {
    return null;
  }
}
