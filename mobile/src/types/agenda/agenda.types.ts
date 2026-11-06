import type { AGENDA_VIEWS } from '../../constants/agenda';

/** A way of looking at the bookings: the list of days, one day, three days, a week or a month. */
export type AgendaView = (typeof AGENDA_VIEWS)[number];

/** What the photographer chose to see in the agenda and what day it is looking at. */
export interface AgendaStore {
  /** The view that is open. */
  readonly view: AgendaView;
  /** A day of the period that is shown; each view turns it into the start of its own period. */
  readonly anchor: Date;
  /** Whether the bookings that are over (completed, client absent, cancelled and expired) are shown too. */
  readonly showAll: boolean;
  /** Changes the view, keeping the day. */
  readonly setView: (view: AgendaView) => void;
  /** Moves the agenda to another day. */
  readonly setAnchor: (anchor: Date) => void;
  /** Moves the agenda to today. */
  readonly goToToday: () => void;
  /** Shows or hides the bookings that are over. */
  readonly setShowAll: (showAll: boolean) => void;
}

/** What is kept on the device between sessions: the view and the switch, not the day, because the agenda always opens on today. */
export interface AgendaPreferences {
  /** The view the agenda opens on. */
  readonly view: AgendaView;
  /** Whether the bookings that are over are shown. */
  readonly showAll: boolean;
}
