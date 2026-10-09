import type { SettlementResponse } from '../api/billing';

/** What the refunds screen can do with its items, as returned by `useRefundActions`. */
export interface RefundActions {
  /** Message of the last failure to open WhatsApp, or null when there is none. */
  readonly error: string | null;
  /** Opens WhatsApp with a message asking the client where to send the refund. */
  readonly contact: (settlement: SettlementResponse) => void;
  /** Opens the form to record the refund as given back. */
  readonly complete: (settlement: SettlementResponse) => void;
  /** Opens the booking the refund is about. */
  readonly openBooking: (settlement: SettlementResponse) => void;
}
