import type { BookingStatus } from '../api/booking';

/** Props of the `StatusBadge` component. */
export interface StatusBadgeProps {
  /** Status to display. */
  readonly status: BookingStatus;
}
