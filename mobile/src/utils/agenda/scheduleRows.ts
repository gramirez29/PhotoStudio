import { SCHEDULE_LOAD_THRESHOLD } from '../../constants/agenda';
import type { DaySection, ScheduleRow, ScrollMetrics } from '../../types/utils/agenda.types';
import { startOfMonth, toDateKey } from './dates';
import { monthName } from './titles';

/**
 * Turns the days of the list view into the rows the list draws: each day is a row, and before the first day of each month there is a
 * banner with its name, which is also how the list says that the month changed. The first row is always a banner.
 * @param sections The days, by date.
 * @param now The current instant, to leave the year out of the banners of the current year.
 * @returns The rows, in order.
 */
export function buildScheduleRows(sections: readonly DaySection[], now: Date): ScheduleRow[] {
  const rows: ScheduleRow[] = [];
  let currentMonth = '';
  for (const section of sections) {
    const month = startOfMonth(section.date);
    const monthKey = toDateKey(month);
    if (monthKey !== currentMonth) {
      currentMonth = monthKey;
      const year = month.getFullYear() === now.getFullYear() ? '' : ` ${month.getFullYear()}`;
      rows.push({ kind: 'month', key: `month-${monthKey}`, label: `${monthName(month)}${year}`, date: month });
    }

    rows.push({ kind: 'day', key: `day-${section.key}`, date: section.date, section });
  }

  return rows;
}

/**
 * Decides, while the list scrolls, whether it is time to load the days before or after the ones loaded. It says nothing when the
 * content is not taller than the screen, because there is nothing to scroll and the buttons at the ends of the list are the way to ask.
 * @param metrics How far the list is scrolled and how big it is.
 * @param threshold Points from an edge at which loading starts.
 * @returns `'start'` near the top, `'end'` near the bottom, or `null` when nothing has to load.
 */
export function pageToLoad(metrics: ScrollMetrics, threshold: number = SCHEDULE_LOAD_THRESHOLD): 'start' | 'end' | null {
  if (metrics.content <= metrics.viewport) {
    return null;
  }

  if (metrics.offset <= threshold) {
    return 'start';
  }

  return metrics.content - (metrics.offset + metrics.viewport) <= threshold ? 'end' : null;
}
