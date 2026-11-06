import { useNavigation } from 'expo-router';
import { useEffect } from 'react';
import { useHomeStore } from '../home/homeStore';
import { replaceScreenWithHome } from '../navigation/homeSwitch';
import type { HomeScreen } from '../types/home/home.types';
import type { ReplaceableNavigation } from '../types/navigation/homeSwitch.types';

/**
 * Makes a home screen follow the choice of the settings while it sits under them: when the photographer picks the other screen as
 * the main one and goes back, the screen they come back to is the one they picked, not the one they left. It does nothing at start-up,
 * where the saved choice is applied by the agenda, and it never touches a screen that was opened on top of another one.
 * @param own The screen that uses this hook.
 */
export function useFollowHomeScreen(own: HomeScreen): void {
  const navigation = useNavigation() as unknown as ReplaceableNavigation;

  useEffect(
    () =>
      useHomeStore.subscribe((state, previous) => {
        if (state.loaded && previous.loaded && state.screen !== previous.screen && state.screen !== own) {
          replaceScreenWithHome(navigation, own, state.screen);
        }
      }),
    [navigation, own],
  );
}
