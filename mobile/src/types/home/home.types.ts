import type { HOME_SCREENS } from '../../constants/home';

/** A screen the app can open on: the agenda or the list of all the bookings. */
export type HomeScreen = (typeof HOME_SCREENS)[number];

/** Which screen the app opens on, and what has already happened with it in this run of the app. */
export interface HomeStore {
  /** The screen the photographer chose. */
  readonly screen: HomeScreen;
  /** Whether the choice saved on the device has been read, so the first screen is not drawn before it is known which one it is. */
  readonly loaded: boolean;
  /** Whether the app already went to the chosen screen in this run, so going back to the agenda later does not bounce away from it. */
  readonly redirected: boolean;
  /** Records the choice. */
  readonly setScreen: (screen: HomeScreen) => void;
  /** Records that the saved choice was read. */
  readonly markLoaded: () => void;
  /** Records that the app went to the chosen screen. */
  readonly markRedirected: () => void;
}
