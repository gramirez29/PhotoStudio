import { HOME_SCREENS } from '../constants/home';
import type { HomeScreen } from '../types/home/home.types';

/** The name of each home screen, as the settings write it. */
export const HOME_SCREEN_LABELS: Readonly<Record<HomeScreen, string>> = {
  agenda: 'Agenda',
  bookings: 'Todas las reservas',
};

/**
 * Tells whether a value is one of the screens the app can open on.
 * @param value Value to check.
 * @returns `true` when it is a home screen.
 */
export function isHomeScreen(value: unknown): value is HomeScreen {
  return typeof value === 'string' && (HOME_SCREENS as readonly string[]).includes(value);
}
