import type { BookingResponse, PaymentResponse } from '../api/booking';

/** Props of the `RejectPaymentForm` component. */
export interface RejectPaymentFormProps {
  /** Booking the proof belongs to. */
  readonly booking: BookingResponse;
  /** Proof being rejected. */
  readonly payment: PaymentResponse;
}
