import * as SecureStore from 'expo-secure-store';
import { AGENDA_PREFERENCES_KEY, loadAgendaPreferences, saveAgendaPreferences } from '../../storage/agendaStorage';
import { changeAgendaShowAll, changeAgendaView, restoreAgendaPreferences } from '../agendaManager';
import { useAgendaStore } from '../agendaStore';

jest.mock('expo-secure-store', () => ({ setItemAsync: jest.fn(), getItemAsync: jest.fn() }));

const setItem = jest.mocked(SecureStore.setItemAsync);
const getItem = jest.mocked(SecureStore.getItemAsync);

describe('useAgendaStore', () => {
  beforeEach(() => {
    jest.useFakeTimers().setSystemTime(new Date(2026, 10, 11, 15, 30));
    useAgendaStore.setState({ view: 'schedule', anchor: new Date(2026, 10, 11), showAll: false });
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('starts on the list of days, showing only what holds the schedule', () => {
    const { view, showAll } = useAgendaStore.getState();

    expect(view).toBe('schedule');
    expect(showAll).toBe(false);
  });

  it('changes the view and keeps the day', () => {
    useAgendaStore.getState().setView('week');

    expect(useAgendaStore.getState().view).toBe('week');
    expect(useAgendaStore.getState().anchor).toEqual(new Date(2026, 10, 11));
  });

  it('moves to another day at midnight, whatever the time it was given', () => {
    useAgendaStore.getState().setAnchor(new Date(2026, 11, 3, 19, 45));

    expect(useAgendaStore.getState().anchor).toEqual(new Date(2026, 11, 3));
  });

  it('goes back to today', () => {
    useAgendaStore.getState().setAnchor(new Date(2027, 0, 1));
    useAgendaStore.getState().goToToday();

    expect(useAgendaStore.getState().anchor).toEqual(new Date(2026, 10, 11));
    expect(useAgendaStore.getState().visibleDate).toEqual(new Date(2026, 10, 11));
  });

  it('looks at the day it was moved to', () => {
    useAgendaStore.getState().setAnchor(new Date(2026, 11, 3, 19, 45));

    expect(useAgendaStore.getState().visibleDate).toEqual(new Date(2026, 11, 3));
  });

  it('follows the scroll with the day it looks at, without moving the anchor', () => {
    useAgendaStore.getState().setVisibleDate(new Date(2026, 11, 20, 8, 30));

    expect(useAgendaStore.getState().visibleDate).toEqual(new Date(2026, 11, 20));
    expect(useAgendaStore.getState().anchor).toEqual(new Date(2026, 10, 11));
  });

  it('shows and hides the bookings that are over', () => {
    useAgendaStore.getState().setShowAll(true);

    expect(useAgendaStore.getState().showAll).toBe(true);
  });
});

describe('agenda storage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('keeps the preferences as text under its key', async () => {
    await saveAgendaPreferences({ view: 'month', showAll: true });

    expect(setItem).toHaveBeenCalledWith(AGENDA_PREFERENCES_KEY, '{"view":"month","showAll":true}');
  });

  it('reads them back, and nothing when what is saved is not valid', async () => {
    getItem.mockResolvedValueOnce('{"view":"month","showAll":true}');
    await expect(loadAgendaPreferences()).resolves.toEqual({ view: 'month', showAll: true });

    getItem.mockResolvedValueOnce('garbage');
    await expect(loadAgendaPreferences()).resolves.toBeNull();

    getItem.mockResolvedValueOnce(null);
    await expect(loadAgendaPreferences()).resolves.toBeNull();
  });

  it('survives a storage that fails, in both directions', async () => {
    setItem.mockRejectedValueOnce(new Error('keystore locked'));
    getItem.mockRejectedValueOnce(new Error('keystore locked'));

    await expect(saveAgendaPreferences({ view: 'day', showAll: false })).resolves.toBeUndefined();
    await expect(loadAgendaPreferences()).resolves.toBeNull();
  });
});

describe('agenda manager', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useAgendaStore.setState({ view: 'schedule', showAll: false });
  });

  it('applies the saved preferences when there are some', async () => {
    getItem.mockResolvedValueOnce('{"view":"week","showAll":true}');

    await restoreAgendaPreferences();

    expect(useAgendaStore.getState()).toMatchObject({ view: 'week', showAll: true });
  });

  it('leaves the defaults when nothing valid is saved', async () => {
    getItem.mockResolvedValueOnce(null);

    await restoreAgendaPreferences();

    expect(useAgendaStore.getState()).toMatchObject({ view: 'schedule', showAll: false });
  });

  it('applies a new view at once and keeps it together with the switch', async () => {
    useAgendaStore.setState({ showAll: true });

    await changeAgendaView('threeDays');

    expect(useAgendaStore.getState().view).toBe('threeDays');
    expect(setItem).toHaveBeenCalledWith(AGENDA_PREFERENCES_KEY, '{"view":"threeDays","showAll":true}');
  });

  it('applies the switch at once and keeps it together with the view', async () => {
    useAgendaStore.setState({ view: 'month' });

    await changeAgendaShowAll(true);

    expect(useAgendaStore.getState().showAll).toBe(true);
    expect(setItem).toHaveBeenCalledWith(AGENDA_PREFERENCES_KEY, '{"view":"month","showAll":true}');
  });
});
