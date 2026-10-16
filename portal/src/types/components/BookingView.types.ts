import type { PortalBooking } from '../api/portal';

/** Props of the `BookingView` component. */
export interface BookingViewProps {
  /** Booking to show. */
  readonly booking: PortalBooking;
}
