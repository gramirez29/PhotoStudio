import { useNavigation } from 'expo-router';
import { useCallback } from 'react';
import { changeHomeScreen } from '../home/homeManager';
import { useHomeStore } from '../home/homeStore';
import { putHomeUnderneath } from '../navigation/homeSwitch';
import type { HomeScreen } from '../types/home/home.types';
import type { ReplaceableNavigation } from '../types/navigation/homeSwitch.types';

/**
 * Gives the function the settings call when the photographer picks the main screen: it keeps the choice for the next time the app
 * opens and, right away, puts the chosen screen under the settings, so going back lands on it.
 * @returns The function that applies a choice.
 */
export function useChooseHomeScreen(): (screen: HomeScreen) => void {
  const navigation = useNavigation() as unknown as ReplaceableNavigation;

  return useCallback(
    (screen: HomeScreen) => {
      if (screen === useHomeStore.getState().screen) {
        return;
      }

      void changeHomeScreen(screen);
      putHomeUnderneath(navigation, screen);
    },
    [navigation],
  );
}
