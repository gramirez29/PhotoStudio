import type { ActionVariant } from '../utils/bookingActions.types';

/** Props of the `ActionButton` component. */
export interface ActionButtonProps {
  /** Text of the button. */
  readonly label: string;
  /** Called when the photographer taps the button. */
  readonly onPress: () => void;
  /** Visual weight of the action; defaults to `primary`. */
  readonly variant?: ActionVariant;
  /** Whether the button ignores taps, for example while a request runs. */
  readonly disabled?: boolean;
}
