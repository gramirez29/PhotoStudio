import type { BookingStatus } from '../api/booking';

/** A slice of the donut chart of the home screen. */
export interface DonutSegment {
  /** The status the slice stands for. */
  readonly status: BookingStatus;
  /** Number of bookings in it. */
  readonly count: number;
  /** Share of the total, from 0 to 1. */
  readonly fraction: number;
  /** Where the slice starts, in degrees from the top, clockwise. */
  readonly startAngle: number;
  /** How far the slice goes, in degrees. */
  readonly sweepAngle: number;
}
