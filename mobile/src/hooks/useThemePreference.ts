import { changeThemePreference } from '../appearance/appearanceManager';
import { useAppearanceStore } from '../appearance/appearanceStore';
import type { ThemePreferenceState } from '../types/hooks/useThemePreference.types';

/**
 * What the photographer chose for the theme and the way to change it. A new choice applies right away and is kept for the next
 * time the app opens.
 * @returns The current choice and the function that sets another one.
 */
export function useThemePreference(): ThemePreferenceState {
  const preference = useAppearanceStore((state) => state.preference);

  return {
    preference,
    choose: (next) => {
      void changeThemePreference(next);
    },
  };
}
