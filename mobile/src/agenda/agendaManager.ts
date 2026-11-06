import { loadAgendaPreferences, saveAgendaPreferences } from '../storage/agendaStorage';
import type { AgendaView } from '../types/agenda/agenda.types';
import { useAgendaStore } from './agendaStore';

/**
 * Takes the preferences saved on the device, if there are any, and applies them. It runs once when the agenda opens; until it answers
 * the agenda shows the default view.
 * @returns A promise that resolves when the saved preferences, if any, are applied.
 */
export async function restoreAgendaPreferences(): Promise<void> {
  const saved = await loadAgendaPreferences();
  if (saved !== null) {
    useAgendaStore.setState({ view: saved.view, showAll: saved.showAll });
  }
}

/**
 * Applies a new view right away and keeps it for the next time the agenda opens.
 * @param view The view to show.
 * @returns A promise that resolves when the choice is kept.
 */
export async function changeAgendaView(view: AgendaView): Promise<void> {
  const store = useAgendaStore.getState();
  store.setView(view);
  // The new view opens on the day that was on screen, not on the day the agenda opened on.
  store.setAnchor(store.visibleDate);
  await saveAgendaPreferences({ view, showAll: useAgendaStore.getState().showAll });
}

/**
 * Shows or hides the bookings that are over right away and keeps the choice for the next time the agenda opens.
 * @param showAll Whether to show them.
 * @returns A promise that resolves when the choice is kept.
 */
export async function changeAgendaShowAll(showAll: boolean): Promise<void> {
  useAgendaStore.getState().setShowAll(showAll);
  await saveAgendaPreferences({ view: useAgendaStore.getState().view, showAll });
}
