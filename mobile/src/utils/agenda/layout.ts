import { MIN_EVENT_MINUTES, MINUTES_PER_DAY } from '../../constants/agenda';
import type { BookingSummaryResponse } from '../../types/api/booking';
import type { PositionedEvent } from '../../types/utils/agenda.types';
import { addDays, startOfDay } from './dates';
import { bookingInterval } from './intervals';

/** A booking clipped to a day, before its column is known. */
interface Draft {
  /** The booking. */
  readonly booking: BookingSummaryResponse;
  /** Minute of the day it starts. */
  readonly start: number;
  /** Minute of the day it ends. */
  readonly end: number;
  /** Minute where its block ends once it has the minimum height. */
  readonly visualEnd: number;
  /** Whether it started on a previous day. */
  readonly continuesBefore: boolean;
  /** Whether it goes on the next day. */
  readonly continuesAfter: boolean;
  /** Column it was put in; set while the group is laid out. */
  column: number;
}

/**
 * Reads the minute of a day an instant falls on, by the clock on the wall, which is what the rows of the grid of hours are. On the days
 * the clock changes, counting the time that went by since midnight would draw everything after the change an hour off.
 * @param instant Instant, in milliseconds.
 * @param dayStart Midnight that starts the day, in milliseconds.
 * @param dayEnd Midnight that ends the day, in milliseconds.
 * @returns 0 for an instant before the day, 1440 for one after it, and the minute on the clock otherwise.
 */
function wallMinutes(instant: number, dayStart: number, dayEnd: number): number {
  if (instant <= dayStart) {
    return 0;
  }

  if (instant >= dayEnd) {
    return MINUTES_PER_DAY;
  }

  const date = new Date(instant);
  return date.getHours() * 60 + date.getMinutes() + date.getSeconds() / 60;
}

/**
 * Tells whether the blocks of two drafts share any minute.
 * @param a A draft.
 * @param b Another draft.
 * @returns `true` when they overlap.
 */
function overlaps(a: Draft, b: Draft): boolean {
  return a.start < b.visualEnd && b.start < a.visualEnd;
}

/**
 * Puts the drafts of one group of overlapping bookings in columns, the way Google Calendar does: each booking goes in the first column
 * that is free when it starts, and then it stretches to the right over the columns that stay free for as long as it lasts.
 * @param group Drafts of the group, ordered by start.
 * @returns The positioned events of the group.
 */
function layoutGroup(group: readonly Draft[]): PositionedEvent[] {
  const columnEnds: number[] = [];
  for (const draft of group) {
    const free = columnEnds.findIndex((end) => end <= draft.start);
    if (free === -1) {
      draft.column = columnEnds.length;
      columnEnds.push(draft.visualEnd);
    } else {
      draft.column = free;
      columnEnds[free] = draft.visualEnd;
    }
  }

  const columns = columnEnds.length;
  return group.map((draft) => {
    let columnSpan = 1;
    for (let next = draft.column + 1; next < columns; next += 1) {
      if (group.some((other) => other.column === next && overlaps(draft, other))) {
        break;
      }

      columnSpan += 1;
    }

    return {
      booking: draft.booking,
      startMinutes: draft.start,
      endMinutes: draft.end,
      visualEndMinutes: draft.visualEnd,
      column: draft.column,
      columnSpan,
      columns,
      continuesBefore: draft.continuesBefore,
      continuesAfter: draft.continuesAfter,
    };
  });
}

/**
 * Places the bookings of a day on the grid of hours. Each booking is clipped to the day (one that started yesterday or goes on tomorrow
 * is drawn from or to the edge), given at least the minimum height, and put side by side with the ones it overlaps, like Google Calendar:
 * bookings that overlap each other, directly or through a third one, share the width in columns, and a booking uses the columns to its
 * right that nothing else uses at that time.
 * @param bookings Bookings that may touch the day; the ones that do not are ignored.
 * @param day Any instant of the day.
 * @param minDuration Minutes a block is drawn for at least; it also decides what overlaps what.
 * @returns The bookings of the day with their place, ordered by start.
 */
export function layoutDay(bookings: readonly BookingSummaryResponse[], day: Date, minDuration: number = MIN_EVENT_MINUTES): PositionedEvent[] {
  const dayStart = startOfDay(day).getTime();
  const dayEnd = addDays(day, 1).getTime();

  const drafts: Draft[] = [];
  for (const booking of bookings) {
    const { start, end } = bookingInterval(booking);
    if (Number.isNaN(start) || Number.isNaN(end) || end <= dayStart || start >= dayEnd) {
      continue;
    }

    const startMinutes = wallMinutes(start, dayStart, dayEnd);
    const endMinutes = wallMinutes(end, dayStart, dayEnd);
    drafts.push({
      booking,
      start: startMinutes,
      end: endMinutes,
      visualEnd: Math.min(MINUTES_PER_DAY, Math.max(endMinutes, startMinutes + minDuration)),
      continuesBefore: start < dayStart,
      continuesAfter: end > dayEnd,
      column: 0,
    });
  }

  drafts.sort((a, b) => a.start - b.start || b.visualEnd - a.visualEnd || (a.booking.id < b.booking.id ? -1 : 1));

  const placed: PositionedEvent[] = [];
  let group: Draft[] = [];
  let groupEnd = Number.NEGATIVE_INFINITY;
  for (const draft of drafts) {
    if (draft.start >= groupEnd && group.length > 0) {
      placed.push(...layoutGroup(group));
      group = [];
    }

    group.push(draft);
    groupEnd = group.length === 1 ? draft.visualEnd : Math.max(groupEnd, draft.visualEnd);
  }

  if (group.length > 0) {
    placed.push(...layoutGroup(group));
  }

  return placed;
}
