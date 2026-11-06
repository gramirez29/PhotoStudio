import { usePathname } from 'expo-router';
import type { HomeScreen } from '../types/home/home.types';

/**
 * Tells which of the two main screens is on the screen right now, so the side menu can mark it.
 * @returns The agenda or the list of bookings, or `null` when another screen is open.
 */
export function useCurrentHomeScreen(): HomeScreen | null {
  const pathname = usePathname();
  if (pathname === '/') {
    return 'agenda';
  }

  return pathname === '/bookings' ? 'bookings' : null;
}
