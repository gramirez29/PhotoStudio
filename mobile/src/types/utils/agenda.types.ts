import type { BookingSummaryResponse } from '../api/booking';

/** A range of days: from the start of the first one, to the start of the day after the last one. Both are midnights of the device time zone. */
export interface DateRange {
  /** First instant of the range. */
  readonly from: Date;
  /** First instant after the range. */
  readonly to: Date;
}

/** A range as the backend takes it: ISO 8601 instants in UTC. */
export interface QueryRange {
  /** First instant of the range. */
  readonly from: string;
  /** First instant after the range. */
  readonly to: string;
}

/** A booking placed on the grid of hours of one day. Times are minutes since the midnight that starts the day. */
export interface PositionedEvent {
  /** The booking. */
  readonly booking: BookingSummaryResponse;
  /** Minute of the day the booking starts, or 0 when it started on a previous day. */
  readonly startMinutes: number;
  /** Minute of the day the booking ends, or 1440 when it goes on the next day. */
  readonly endMinutes: number;
  /** Minute where the block ends once it has at least the minimum height; this is what decides what overlaps what. */
  readonly visualEndMinutes: number;
  /** Column the block is in, from the left, among the ones that overlap it. */
  readonly column: number;
  /** How many columns the block takes: more than one when the columns to its right are free. */
  readonly columnSpan: number;
  /** How many columns there are in the group of bookings that overlap each other. */
  readonly columns: number;
  /** Whether the booking started on a previous day. */
  readonly continuesBefore: boolean;
  /** Whether the booking goes on the next day. */
  readonly continuesAfter: boolean;
}

/** One day of the list view: the day and the bookings that touch it. */
export interface DaySection {
  /** Midnight that starts the day. */
  readonly date: Date;
  /** The day as `YYYY-MM-DD`, to identify it. */
  readonly key: string;
  /** Whether the day is today. */
  readonly isToday: boolean;
  /** The bookings that touch the day, by start. Empty only for today. */
  readonly bookings: readonly BookingSummaryResponse[];
}

/** One day of the grid of the month. */
export interface MonthCell {
  /** Midnight that starts the day. */
  readonly date: Date;
  /** The day as `YYYY-MM-DD`, to identify it. */
  readonly key: string;
  /** Whether the day belongs to the month that is shown (the cells around it belong to the previous or the next). */
  readonly inMonth: boolean;
  /** Whether the day is today. */
  readonly isToday: boolean;
  /** The bookings that touch the day, by start. */
  readonly bookings: readonly BookingSummaryResponse[];
}

/** One row of the list view: the banner of a month, or a day with its bookings. */
export type ScheduleRow =
  | { readonly kind: 'month'; readonly key: string; readonly label: string; readonly date: Date }
  | { readonly kind: 'day'; readonly key: string; readonly date: Date; readonly section: DaySection };

/** What a list reports while it scrolls: how far it is and how big it is, all in points. */
export interface ScrollMetrics {
  /** Distance scrolled from the top of the content. */
  readonly offset: number;
  /** Height of the visible part of the list. */
  readonly viewport: number;
  /** Height of the whole content. */
  readonly content: number;
}

/** Where the block of a booking goes inside the column of its day, in points. */
export interface EventFrame {
  /** Distance from the top of the column. */
  readonly top: number;
  /** Height of the block. */
  readonly height: number;
  /** Distance from the left of the column. */
  readonly left: number;
  /** Width of the block. */
  readonly width: number;
}
