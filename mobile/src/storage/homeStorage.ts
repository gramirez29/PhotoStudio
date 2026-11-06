import * as SecureStore from 'expo-secure-store';
import type { HomeScreen } from '../types/home/home.types';
import { isHomeScreen } from '../utils/home';

/** Key under which the choice of home screen is kept on the device. */
export const HOME_SCREEN_KEY = 'photostudio.homeScreen';

/**
 * Saves the choice of home screen. A failure is swallowed: the choice keeps working until the app closes.
 * @param screen What to keep.
 * @returns A promise that resolves when the attempt finished.
 */
export async function saveHomeScreen(screen: HomeScreen): Promise<void> {
  try {
    await SecureStore.setItemAsync(HOME_SCREEN_KEY, screen);
  } catch {
    // The storage can be unavailable; the choice stays in memory.
  }
}

/**
 * Reads the saved choice of home screen.
 * @returns The choice, or `null` when none is saved, it is not valid or the storage cannot be read.
 */
export async function loadHomeScreen(): Promise<HomeScreen | null> {
  try {
    const value = await SecureStore.getItemAsync(HOME_SCREEN_KEY);
    return isHomeScreen(value) ? value : null;
  } catch {
    return null;
  }
}
