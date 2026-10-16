import type { Money } from '../api/portal';

/** Props of the `AmountRow` component. */
export interface AmountRowProps {
  /** What the amount is. */
  readonly label: string;
  /** The amount. */
  readonly money: Money;
  /** Whether the row stands out, for the figure that matters most. */
  readonly emphasized?: boolean;
}
