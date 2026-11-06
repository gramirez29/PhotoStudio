import { IMPLEMENTED_AGENDA_VIEWS } from '../../constants/agenda';
import type { AgendaView } from '../../types/agenda/agenda.types';

/**
 * Tells whether a view already has a screen.
 * @param view A view.
 * @returns `true` when the agenda can show it.
 */
export function isViewImplemented(view: AgendaView): boolean {
  return (IMPLEMENTED_AGENDA_VIEWS as readonly string[]).includes(view);
}

/**
 * Gets the view the agenda really shows for the one that is chosen: the same, or the list when the chosen one has no screen yet (a
 * preference saved by a newer version of the app, for example).
 * @param view The chosen view.
 * @returns A view that has a screen.
 */
export function effectiveAgendaView(view: AgendaView): AgendaView {
  return isViewImplemented(view) ? view : 'schedule';
}
