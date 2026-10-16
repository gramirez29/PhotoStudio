import type { BookingResponse } from '../api/booking';

/** Props of the `ShareLinkCard` component. */
export interface ShareLinkCardProps {
  /** Booking whose link is shared. */
  readonly booking: BookingResponse;
}
