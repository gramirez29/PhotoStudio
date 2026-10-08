import type { BookingResponse } from '../api/booking';

/** Props of the `BookingDetails` component. */
export interface BookingDetailsProps {
  /** Booking to display. */
  readonly booking: BookingResponse;
}
