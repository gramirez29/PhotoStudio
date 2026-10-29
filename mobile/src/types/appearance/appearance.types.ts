import type { ThemePreference } from '../theme/palette.types';

/** State of the appearance of the app: what the photographer chose for the theme. It holds no side effects. */
export interface AppearanceStore {
  /** What the photographer chose: follow the phone, or always light, or always dark. */
  readonly preference: ThemePreference;
  /** Replaces the choice in memory. */
  readonly setPreference: (preference: ThemePreference) => void;
}
