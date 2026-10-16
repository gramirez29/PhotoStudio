import type { PortalPayment } from '../api/portal';

/** Props of the `PaymentsList` component. */
export interface PaymentsListProps {
  /** Payments to list. */
  readonly payments: readonly PortalPayment[];
}
