import type { PortalBooking, PortalSession } from '../api/portal';

/** Props of the `ProofFlow` component. */
export interface ProofFlowProps {
  /** Secret token of the booking link. */
  readonly token: string;
  /** Booking as the API last described it. */
  readonly booking: PortalBooking;
  /** Session the page already has (the client verified a code for another action), or null. */
  readonly session: PortalSession | null;
  /** Called when a code is verified or the session stops working, so the other flows of the page share it. */
  readonly onSessionChange: (session: PortalSession | null) => void;
}
