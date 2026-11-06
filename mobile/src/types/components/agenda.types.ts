import type { BookingSummaryResponse } from '../api/booking';
import type { DaySection } from '../utils/agenda.types';

/** Props of the `AgendaEventChip` component. */
export interface AgendaEventChipProps {
  /** The booking to show. */
  readonly booking: BookingSummaryResponse;
  /** Called with the identifier of the booking when it is tapped. */
  readonly onPress: (bookingId: string) => void;
}

/** Props of the `ScheduleDayRow` component. */
export interface ScheduleDayRowProps {
  /** The day and its bookings. */
  readonly section: DaySection;
  /** Called with the identifier of a booking when it is tapped. */
  readonly onOpenBooking: (bookingId: string) => void;
}

/** Props of the `ScheduleMonthBanner` component. */
export interface ScheduleMonthBannerProps {
  /** The name of the month, with the year when it is not the current one. */
  readonly label: string;
}

/** Props of the `ScheduleEdgeButton` component. */
export interface ScheduleEdgeButtonProps {
  /** What the button says. */
  readonly label: string;
  /** Called when it is tapped. */
  readonly onPress: () => void;
  /** Whether what it loads is loading right now. */
  readonly loading: boolean;
}

/** Props of the `ScheduleView` component. */
export interface ScheduleViewProps {
  /** The day the list opens on. The list restarts when it changes. */
  readonly anchor: Date;
  /** Whether the bookings that are over are shown too. */
  readonly includeEnded: boolean;
}

/** Props of the `AgendaViewMenu` component. */
export interface AgendaViewMenuProps {
  /** Whether the menu is open. */
  readonly visible: boolean;
  /** Called when the menu has to close. */
  readonly onClose: () => void;
}
