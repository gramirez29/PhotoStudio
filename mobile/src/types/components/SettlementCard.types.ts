import type { BookingResponse } from '../api/booking';

/** Props of the `SettlementCard` component. */
export interface SettlementCardProps {
  /** Booking whose money is shown. */
  readonly booking: BookingResponse;
}
