import type { BookingSummaryResponse } from '../api/booking';

/** Props of the `BookingListItem` component. */
export interface BookingListItemProps {
  /** Booking to display. */
  readonly booking: BookingSummaryResponse;
  /** Called when the photographer taps the card. */
  readonly onPress: (bookingId: string) => void;
}
