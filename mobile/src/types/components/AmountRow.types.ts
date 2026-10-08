import type { MoneyResponse } from '../api/booking';

/** Props of the `AmountRow` component. */
export interface AmountRowProps {
  /** Label shown on the left. */
  readonly label: string;
  /** Amount shown on the right. */
  readonly money: MoneyResponse;
  /** Whether to emphasize the row (for example, the balance). */
  readonly emphasized?: boolean;
}
