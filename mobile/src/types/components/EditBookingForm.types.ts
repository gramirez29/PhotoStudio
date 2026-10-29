import type { BookingResponse } from '../api/booking';

/** Props of the `EditBookingForm` component. */
export interface EditBookingFormProps {
  /** Booking being corrected. */
  readonly booking: BookingResponse;
}
