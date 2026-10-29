import { loadThemePreference, saveThemePreference } from '../storage/appearanceStorage';
import type { ThemePreference } from '../types/theme/palette.types';
import { useAppearanceStore } from './appearanceStore';

/**
 * Takes the choice saved on the device, if there is one, and applies it. It runs once when the app opens; until it answers the
 * app shows the default theme.
 * @returns A promise that resolves when the saved choice, if any, is applied.
 */
export async function restoreAppearance(): Promise<void> {
  const saved = await loadThemePreference();
  if (saved !== null) {
    useAppearanceStore.getState().setPreference(saved);
  }
}

/**
 * Applies a new choice of theme right away and keeps it for the next time the app opens.
 * @param preference The choice.
 * @returns A promise that resolves when the choice is kept.
 */
export async function changeThemePreference(preference: ThemePreference): Promise<void> {
  useAppearanceStore.getState().setPreference(preference);
  await saveThemePreference(preference);
}
