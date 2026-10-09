import type { BookingResponse } from '../api/booking';

/** Props of the `PaymentForm` component. */
export interface PaymentFormProps {
  /** Booking the payment is recorded on. */
  readonly booking: BookingResponse;
}
