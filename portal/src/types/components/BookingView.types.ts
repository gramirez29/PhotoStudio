import type { ReactNode } from 'react';
import type { PortalBooking } from '../api/portal';

/** Props of the `BookingView` component. */
export interface BookingViewProps {
  /** Booking to show. */
  readonly booking: PortalBooking;
  /** What the client can do next (the contract and the payment proof flows), shown right after what comes next. */
  readonly action?: ReactNode;
}
