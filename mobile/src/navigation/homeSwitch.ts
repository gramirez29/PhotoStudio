import type { HomeScreen } from '../types/home/home.types';
import type { ReplaceableNavigation } from '../types/navigation/homeSwitch.types';

/** The name of the route of each home screen. */
const ROUTE_NAMES: Readonly<Record<HomeScreen, string>> = {
  agenda: 'index',
  bookings: 'bookings/index',
};

/**
 * Replaces a home screen with the other one in the stack, without moving the photographer: the screen on top (the settings) stays
 * where it is and, going back, the one below is the new one. It is done only when the stack has no other copy of the target, and, for
 * the list of bookings, only when it is the first screen of the stack, so one that was opened on top of the agenda is left alone.
 * @param navigation Navigation of the screen that is replaced.
 * @param own The screen that is replaced.
 * @param target The screen that takes its place.
 */
export function replaceScreenWithHome(navigation: ReplaceableNavigation, own: HomeScreen, target: HomeScreen): void {
  const routes = navigation.getState()?.routes ?? [];
  const targetName = ROUTE_NAMES[target];
  if (routes.some((route) => route.name === targetName)) {
    return;
  }

  if (own === 'bookings' && routes[0]?.name !== ROUTE_NAMES.bookings) {
    return;
  }

  navigation.dispatch({ type: 'REPLACE', payload: { name: targetName } });
}
