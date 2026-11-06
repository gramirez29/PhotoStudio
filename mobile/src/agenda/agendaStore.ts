import { create } from 'zustand';
import { DEFAULT_AGENDA_VIEW } from '../constants/agenda';
import type { AgendaStore } from '../types/agenda/agenda.types';
import { startOfDay } from '../utils/agenda/dates';

/**
 * State of the agenda: the view that is open, the day it looks at and whether the bookings that are over are shown. It holds no side
 * effects; the agenda manager keeps what has to survive between sessions. It starts on today, on the default view, showing only the
 * bookings that still hold the schedule.
 */
export const useAgendaStore = create<AgendaStore>((set) => ({
  view: DEFAULT_AGENDA_VIEW,
  anchor: startOfDay(new Date()),
  visibleDate: startOfDay(new Date()),
  showAll: false,
  setView: (view) => set({ view }),
  setAnchor: (anchor) => set({ anchor: startOfDay(anchor), visibleDate: startOfDay(anchor) }),
  setVisibleDate: (visibleDate) => set({ visibleDate: startOfDay(visibleDate) }),
  goToToday: () => set({ anchor: startOfDay(new Date()), visibleDate: startOfDay(new Date()) }),
  setShowAll: (showAll) => set({ showAll }),
}));
