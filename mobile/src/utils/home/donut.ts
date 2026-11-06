import type { BookingStatsResponse } from '../../types/api/stats';
import type { DonutSegment } from '../../types/utils/home.types';

/**
 * Turns the counts of the bookings into the slices of the donut chart. Only the statuses that have bookings get a slice, in the order
 * the backend sends them, and each one takes the share of the circle that its count is of the total. Angles are in degrees, from 0 at
 * the top, clockwise.
 * @param stats The counts by status.
 * @returns The slices; none when there are no bookings.
 */
export function donutSegments(stats: BookingStatsResponse): DonutSegment[] {
  if (stats.total <= 0) {
    return [];
  }

  let start = 0;
  return stats.byStatus
    .filter((entry) => entry.count > 0)
    .map((entry) => {
      const fraction = entry.count / stats.total;
      const segment = { status: entry.status, count: entry.count, fraction, startAngle: start, sweepAngle: fraction * 360 };
      start += segment.sweepAngle;
      return segment;
    });
}

/**
 * Writes a share of the total as a percentage for the legend, for example "33 %".
 * @param fraction A number from 0 to 1.
 * @returns The percentage rounded to a whole number.
 */
export function formatPercent(fraction: number): string {
  return `${Math.round(fraction * 100)} %`;
}
