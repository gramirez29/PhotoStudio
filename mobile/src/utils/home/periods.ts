import type { HomePeriodKey } from '../../types/hooks/useBookingStats.types';
import type { QueryRange } from '../../types/utils/agenda.types';
import { addMonths, startOfMonth } from '../agenda/dates';
import { toQueryRange } from '../agenda/ranges';

/** The name of each period, as the selector of the home screen writes it. */
export const HOME_PERIOD_LABELS: Readonly<Record<HomePeriodKey, string>> = {
  thisMonth: 'Este mes',
  lastMonth: 'Mes pasado',
  all: 'Todo',
};

/**
 * Gets the range of dates a period of the home screen counts. A booking belongs to a period by the day its session starts, in the time
 * of the phone.
 * @param period The period.
 * @param now The current instant.
 * @returns The range as the backend takes it, or `null` for all the bookings.
 */
export function periodRange(period: HomePeriodKey, now: Date): QueryRange | null {
  const thisMonth = startOfMonth(now);
  switch (period) {
    case 'thisMonth':
      return toQueryRange({ from: thisMonth, to: addMonths(thisMonth, 1) });
    case 'lastMonth':
      return toQueryRange({ from: addMonths(thisMonth, -1), to: thisMonth });
    default:
      return null;
  }
}
