import type { BookingResponse } from '../api/booking';

/** Props of the `PendingPaymentsCard` component. */
export interface PendingPaymentsCardProps {
  /** Booking whose payment proofs are shown. */
  readonly booking: BookingResponse;
}
