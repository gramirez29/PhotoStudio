import type { BookingResponse } from '../api/booking';

/** Props of the `ContractForm` component. */
export interface ContractFormProps {
  /** Booking whose contract is signed. */
  readonly booking: BookingResponse;
}
