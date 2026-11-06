import * as SecureStore from 'expo-secure-store';
import { HOME_SCREEN_KEY } from '../../storage/homeStorage';
import { changeHomeScreen, restoreHomeScreen } from '../homeManager';
import { useHomeStore } from '../homeStore';

jest.mock('expo-secure-store', () => ({ getItemAsync: jest.fn(), setItemAsync: jest.fn() }));

describe('homeManager', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useHomeStore.setState({ screen: 'agenda', loaded: false, redirected: false });
  });

  it('applies the saved choice and says it is known', async () => {
    jest.mocked(SecureStore.getItemAsync).mockResolvedValue('bookings');

    await restoreHomeScreen();

    expect(useHomeStore.getState()).toMatchObject({ screen: 'bookings', loaded: true });
  });

  it('keeps the agenda when nothing valid is saved, or the storage fails', async () => {
    jest.mocked(SecureStore.getItemAsync).mockResolvedValue('nonsense');
    await restoreHomeScreen();
    expect(useHomeStore.getState()).toMatchObject({ screen: 'agenda', loaded: true });

    useHomeStore.setState({ loaded: false });
    jest.mocked(SecureStore.getItemAsync).mockRejectedValue(new Error('locked'));
    await restoreHomeScreen();
    expect(useHomeStore.getState()).toMatchObject({ screen: 'agenda', loaded: true });
  });

  it('applies a new choice and keeps it on the device', async () => {
    await changeHomeScreen('bookings');

    expect(useHomeStore.getState().screen).toBe('bookings');
    expect(SecureStore.setItemAsync).toHaveBeenCalledWith(HOME_SCREEN_KEY, 'bookings');
  });
});
