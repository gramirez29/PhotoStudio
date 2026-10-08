import type { BookingResponse } from '../api/booking';

/** Props of the `BookingActions` component. */
export interface BookingActionsProps {
  /** Booking whose allowed actions are shown. */
  readonly booking: BookingResponse;
}
