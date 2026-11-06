import { usePathname } from 'expo-router';
import type { MainScreen } from '../types/navigation/mainScreen.types';

/** The path of each main screen. */
const PATHS: Readonly<Record<string, MainScreen>> = {
  '/': 'home',
  '/agenda': 'agenda',
  '/bookings': 'bookings',
};

/**
 * Tells which of the main screens is on the screen right now, so the side menu can mark it.
 * @returns The main screen that is open, or `null` when another screen is open.
 */
export function useCurrentMainScreen(): MainScreen | null {
  return PATHS[usePathname()] ?? null;
}
