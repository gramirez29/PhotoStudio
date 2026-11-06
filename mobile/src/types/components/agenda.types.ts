import type { AgendaView } from '../agenda/agenda.types';
import type { BookingSummaryResponse } from '../api/booking';
import type { DaySection, MonthCell, PositionedEvent } from '../utils/agenda.types';

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

/** Props of the `TimeGridView` component. */
export interface TimeGridViewProps {
  /** The view: the day, the three days or the week. */
  readonly view: AgendaView;
  /** The day the grid opens on. The grid restarts when it changes. */
  readonly anchor: Date;
  /** Whether the bookings that are over are shown too. */
  readonly includeEnded: boolean;
}

/** Props of the `TimeGridPage` component. */
export interface TimeGridPageProps {
  /** The days the page shows, in order. */
  readonly days: readonly Date[];
  /** All the bookings loaded around the page; each day takes the ones that touch it. */
  readonly bookings: readonly BookingSummaryResponse[];
  /** Width of the page, in points. */
  readonly width: number;
  /** Gives the distance the hours are scrolled right now, so a page that appears shows the same hours as the one before. */
  readonly getOffset: () => number;
  /** Called with the distance the hours are scrolled whenever the photographer scrolls them. */
  readonly onScrollOffset: (offset: number) => void;
  /** Called with the id of the booking the photographer touches. */
  readonly onOpenBooking: (bookingId: string) => void;
}

/** Props of the `TimeGridColumn` component. */
export interface TimeGridColumnProps {
  /** Midnight that starts the day of the column. */
  readonly day: Date;
  /** Bookings that may touch the day. */
  readonly bookings: readonly BookingSummaryResponse[];
  /** Width of the column, in points. */
  readonly width: number;
  /** Called with the id of the booking the photographer touches. */
  readonly onOpenBooking: (bookingId: string) => void;
}

/** Props of the `TimeGridEvent` component. */
export interface TimeGridEventProps {
  /** The booking placed on the day. */
  readonly event: PositionedEvent;
  /** Width of the column of the day, in points. */
  readonly columnWidth: number;
  /** Called with the id of the booking when it is touched. */
  readonly onPress: (bookingId: string) => void;
}

/** Props of the `MonthView` component. */
export interface MonthViewProps {
  /** A day of the month the view opens on. The view restarts when it changes. */
  readonly anchor: Date;
  /** Whether the bookings that are over are shown too. */
  readonly includeEnded: boolean;
}

/** Props of the `MonthPage` component. */
export interface MonthPageProps {
  /** A day of the month of the page. */
  readonly month: Date;
  /** All the bookings loaded around the page. */
  readonly bookings: readonly BookingSummaryResponse[];
  /** Width of the page, in points. */
  readonly width: number;
  /** Height of the page, in points. */
  readonly height: number;
  /** Called with the day the photographer touches. */
  readonly onOpenDay: (day: Date) => void;
}

/** Props of the `MonthDayCell` component. */
export interface MonthDayCellProps {
  /** The day with its bookings. */
  readonly cell: MonthCell;
  /** Width of the cell, in points. */
  readonly width: number;
  /** Height of the cell, in points. */
  readonly height: number;
  /** Called with the day when the cell is touched. */
  readonly onPress: (day: Date) => void;
}

/** Props of the `MiniMonthPicker` component. */
export interface MiniMonthPickerProps {
  /** Whether the picker is open. */
  readonly visible: boolean;
  /** Called when the picker has to close. */
  readonly onClose: () => void;
}
