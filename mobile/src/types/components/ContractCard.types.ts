import type { BookingResponse } from '../api/booking';

/** Props of the `ContractCard` component. */
export interface ContractCardProps {
  /** Booking whose contract is shown. */
  readonly booking: BookingResponse;
}
