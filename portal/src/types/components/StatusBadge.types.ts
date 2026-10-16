import type { StatusTone } from '../utils/labels.types';

/** Props of the `StatusBadge` component. */
export interface StatusBadgeProps {
  /** Text of the status. */
  readonly label: string;
  /** Tone of the dot next to the text. */
  readonly tone: StatusTone;
}
