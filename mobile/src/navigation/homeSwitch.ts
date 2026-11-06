import type { HomeScreen } from '../types/home/home.types';
import type { ReplaceableNavigation } from '../types/navigation/homeSwitch.types';

/** The name of the route of each home screen. */
const ROUTE_NAMES: Readonly<Record<HomeScreen, string>> = {
  agenda: 'index',
  bookings: 'bookings/index',
};

/**
 * Makes a home screen the first one of the stack and keeps the screen on top exactly as it is. The settings use it when the
 * photographer picks the main screen: the settings stay on the screen, and going back lands on the one that was picked, whatever
 * the stack had underneath.
 * @param navigation Navigation of the screen that is on top.
 * @param target The home screen that goes to the bottom of the stack.
 */
export function putHomeUnderneath(navigation: ReplaceableNavigation, target: HomeScreen): void {
  const routes = navigation.getState()?.routes ?? [];
  const top = routes[routes.length - 1];
  if (top === undefined) {
    return;
  }

  navigation.reset({ index: 1, routes: [{ name: ROUTE_NAMES[target] }, top] });
}
