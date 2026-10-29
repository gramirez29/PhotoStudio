import * as SecureStore from 'expo-secure-store';
import { loadThemePreference, saveThemePreference, THEME_PREFERENCE_KEY } from '../../storage/appearanceStorage';
import { changeThemePreference, restoreAppearance } from '../appearanceManager';
import { useAppearanceStore } from '../appearanceStore';

jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(),
  setItemAsync: jest.fn(),
  deleteItemAsync: jest.fn(),
}));

describe('the appearance of the app', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useAppearanceStore.setState({ preference: 'light' });
  });

  it('starts in light, not in what the phone says, so everyone sees the same until they choose', () => {
    expect(useAppearanceStore.getState().preference).toBe('light');
  });

  describe('storage', () => {
    it('keeps and reads the choice', async () => {
      jest.mocked(SecureStore.getItemAsync).mockResolvedValue('dark');

      await saveThemePreference('dark');

      expect(SecureStore.setItemAsync).toHaveBeenCalledWith(THEME_PREFERENCE_KEY, 'dark');
      expect(await loadThemePreference()).toBe('dark');
    });

    it('ignores what is saved when it is not a choice the app offers', async () => {
      jest.mocked(SecureStore.getItemAsync).mockResolvedValue('purple');

      expect(await loadThemePreference()).toBeNull();
    });

    it('survives a storage that fails, reading and writing', async () => {
      jest.mocked(SecureStore.getItemAsync).mockRejectedValue(new Error('locked'));
      jest.mocked(SecureStore.setItemAsync).mockRejectedValue(new Error('locked'));

      expect(await loadThemePreference()).toBeNull();
      await expect(saveThemePreference('dark')).resolves.toBeUndefined();
    });
  });

  describe('manager', () => {
    it('applies the saved choice when the app opens', async () => {
      jest.mocked(SecureStore.getItemAsync).mockResolvedValue('system');

      await restoreAppearance();

      expect(useAppearanceStore.getState().preference).toBe('system');
    });

    it('keeps the default when nothing is saved', async () => {
      jest.mocked(SecureStore.getItemAsync).mockResolvedValue(null);

      await restoreAppearance();

      expect(useAppearanceStore.getState().preference).toBe('light');
    });

    it('applies a new choice right away and keeps it for the next time', async () => {
      await changeThemePreference('dark');

      expect(useAppearanceStore.getState().preference).toBe('dark');
      expect(SecureStore.setItemAsync).toHaveBeenCalledWith(THEME_PREFERENCE_KEY, 'dark');
    });

    it('applies a new choice even when it cannot be kept', async () => {
      jest.mocked(SecureStore.setItemAsync).mockRejectedValue(new Error('locked'));

      await changeThemePreference('dark');

      expect(useAppearanceStore.getState().preference).toBe('dark');
    });
  });
});
