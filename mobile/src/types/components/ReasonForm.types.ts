import type { BookingResponse } from '../api/booking';
import type { ReasonAction } from '../utils/bookingActions.types';

/** Props of the `ReasonForm` component. */
export interface ReasonFormProps {
  /** Booking the action is performed on. */
  readonly booking: BookingResponse;
  /** Action that needs a reason. */
  readonly action: ReasonAction;
}
