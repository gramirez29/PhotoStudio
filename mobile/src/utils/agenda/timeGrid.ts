import { GRID_DEFAULT_OPEN_MINUTES, GRID_OPEN_BEFORE_NOW_MINUTES, HOUR_HEIGHT, MINUTES_PER_DAY } from '../../constants/agenda';
import type { AgendaView } from '../../types/agenda/agenda.types';
import type { EventFrame, PositionedEvent } from '../../types/utils/agenda.types';
import { addDays, minutesSinceMidnight } from './dates';
import { periodStart, shiftAnchor } from './ranges';

/** Horizontal space left between two blocks side by side, and at the right of the column, in points. */
const EVENT_GAP = 2;

/** Views that draw the grid of hours, with the days each page shows. */
const GRID_DAY_COUNT: Readonly<Partial<Record<AgendaView, number>>> = { day: 1, threeDays: 3, week: 7 };

/**
 * Tells how many days a page of a view shows.
 * @param view The view.
 * @returns 1, 3 or 7 for the views of the grid of hours; 1 for any other.
 */
export function gridDayCount(view: AgendaView): number {
  return GRID_DAY_COUNT[view] ?? 1;
}

/**
 * Gets the days of a page of the grid.
 * @param view The view.
 * @param origin The day the grid opened on, which is in the page 0.
 * @param page Pages from the first one; negative to go back.
 * @returns The midnights of the days of the page, in order.
 */
export function gridPageDays(view: AgendaView, origin: Date, page: number): Date[] {
  const start = periodStart(view, shiftAnchor(view, origin, page));
  return Array.from({ length: gridDayCount(view) }, (_, index) => addDays(start, index));
}

/**
 * Writes the label of an hour of the grid, for example "3 p. m.".
 * @param hour 0 to 23.
 * @returns The label; the first hour of the day has none.
 */
export function hourLabel(hour: number): string {
  if (hour === 0) {
    return '';
  }

  const twelve = hour % 12 === 0 ? 12 : hour % 12;
  return `${twelve} ${hour < 12 ? 'a. m.' : 'p. m.'}`;
}

/**
 * Turns a minute of the day into a distance from the top of the grid.
 * @param minutes Minutes since the midnight that starts the day.
 * @returns Points.
 */
export function minutesToOffset(minutes: number): number {
  return (minutes / 60) * HOUR_HEIGHT;
}

/**
 * Works out where the block of a booking goes in the column of its day: its height is its real duration (at least the minimum the
 * layout gave it) and the width is shared with the blocks next to it.
 * @param event The booking placed by the layout of the day.
 * @param columnWidth Width of the column of the day, in points.
 * @returns The frame of the block.
 */
export function eventFrame(event: PositionedEvent, columnWidth: number): EventFrame {
  const slot = columnWidth / event.columns;
  return {
    top: minutesToOffset(event.startMinutes),
    height: minutesToOffset(event.visualEndMinutes - event.startMinutes),
    left: slot * event.column,
    width: Math.max(0, slot * event.columnSpan - EVENT_GAP),
  };
}

/**
 * Chooses how far the grid is scrolled when it opens: a little before the current time when the page has today, so what is going on
 * is in view, and at the start of the working day otherwise.
 * @param showsToday Whether the page that opens has today.
 * @param now The current instant.
 * @returns A distance from the top of the grid, in points; never negative.
 */
export function initialGridOffset(showsToday: boolean, now: Date): number {
  const minutes = showsToday ? minutesSinceMidnight(now) - GRID_OPEN_BEFORE_NOW_MINUTES : GRID_DEFAULT_OPEN_MINUTES;
  return minutesToOffset(Math.min(Math.max(minutes, 0), MINUTES_PER_DAY));
}
