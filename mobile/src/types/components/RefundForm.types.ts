import type { SettlementResponse } from '../api/billing';

/** Props of the `RefundForm` component. */
export interface RefundFormProps {
  /** Settlement whose refund is recorded as given back. */
  readonly settlement: SettlementResponse;
}
