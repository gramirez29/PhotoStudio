import { loadHomeScreen, saveHomeScreen } from '../storage/homeStorage';
import type { HomeScreen } from '../types/home/home.types';
import { useHomeStore } from './homeStore';

/**
 * Takes the choice saved on the device, if there is one, and applies it. It runs once per run of the app, when the first screen opens;
 * whatever happens, it ends by saying that the choice is known.
 * @returns A promise that resolves when the saved choice, if any, is applied.
 */
export async function restoreHomeScreen(): Promise<void> {
  const saved = await loadHomeScreen();
  const store = useHomeStore.getState();
  if (saved !== null) {
    store.setScreen(saved);
  }

  store.markLoaded();
}

/**
 * Applies a new choice of home screen and keeps it for the next time the app opens. It does not move the photographer: the choice
 * applies the next time the app opens.
 * @param screen The choice.
 * @returns A promise that resolves when the choice is kept.
 */
export async function changeHomeScreen(screen: HomeScreen): Promise<void> {
  useHomeStore.getState().setScreen(screen);
  await saveHomeScreen(screen);
}
