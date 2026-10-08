import type { BookingResponse } from '../api/booking';

/** Props of the `RescheduleForm` component. */
export interface RescheduleFormProps {
  /** Booking being moved. */
  readonly booking: BookingResponse;
}
