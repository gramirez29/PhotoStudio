import * as SecureStore from 'expo-secure-store';
import type { AgendaPreferences } from '../types/agenda/agenda.types';
import { parseAgendaPreferences } from '../utils/agenda/preferences';

/** Key under which the preferences of the agenda are kept on the device. */
export const AGENDA_PREFERENCES_KEY = 'photostudio.agendaPreferences';

/**
 * Saves the preferences of the agenda. A failure is swallowed: they keep working until the app closes and the photographer only has to
 * choose again.
 * @param preferences What to keep.
 * @returns A promise that resolves when the attempt finished.
 */
export async function saveAgendaPreferences(preferences: AgendaPreferences): Promise<void> {
  try {
    await SecureStore.setItemAsync(AGENDA_PREFERENCES_KEY, JSON.stringify(preferences));
  } catch {
    // The storage can be unavailable; the preferences stay in memory.
  }
}

/**
 * Reads the saved preferences of the agenda.
 * @returns The preferences, or `null` when none are saved, they are not valid or the storage cannot be read.
 */
export async function loadAgendaPreferences(): Promise<AgendaPreferences | null> {
  try {
    return parseAgendaPreferences(await SecureStore.getItemAsync(AGENDA_PREFERENCES_KEY));
  } catch {
    return null;
  }
}
