import type { ThemePreference } from '../theme/palette.types';

/** What `useThemePreference` gives a component: the choice of theme and the way to change it. */
export interface ThemePreferenceState {
  /** What the photographer chose: follow the phone, or always light, or always dark. */
  readonly preference: ThemePreference;
  /** Applies a new choice right away and keeps it for the next time the app opens. */
  readonly choose: (preference: ThemePreference) => void;
}
