import { AGENDA_VIEWS } from '../../constants/agenda';
import type { AgendaPreferences, AgendaView } from '../../types/agenda/agenda.types';

/**
 * Tells whether a value is one of the views of the agenda.
 * @param value Value to check.
 * @returns `true` when it is a view.
 */
export function isAgendaView(value: unknown): value is AgendaView {
  return typeof value === 'string' && (AGENDA_VIEWS as readonly string[]).includes(value);
}

/**
 * Reads the preferences of the agenda from the text that was kept on the device. Anything that is not exactly what was written, such
 * as text from an older version or a damaged value, is discarded instead of half-applied.
 * @param raw The text read from storage, or `null` when there is none.
 * @returns The preferences, or `null` when there are none or they are not valid.
 */
export function parseAgendaPreferences(raw: string | null): AgendaPreferences | null {
  if (raw === null) {
    return null;
  }

  try {
    const value: unknown = JSON.parse(raw);
    if (typeof value !== 'object' || value === null) {
      return null;
    }

    const record = value as Record<string, unknown>;
    return isAgendaView(record.view) && typeof record.showAll === 'boolean' ? { view: record.view, showAll: record.showAll } : null;
  } catch {
    return null;
  }
}
