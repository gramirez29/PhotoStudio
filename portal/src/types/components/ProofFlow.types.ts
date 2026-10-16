import type { PortalBooking } from '../api/portal';

/** Props of the `ProofFlow` component. */
export interface ProofFlowProps {
  /** Secret token of the booking link. */
  readonly token: string;
  /** Booking as the API last described it. */
  readonly booking: PortalBooking;
}
