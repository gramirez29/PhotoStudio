import { fireEvent, render, screen } from '@testing-library/react-native';
import { StyleSheet } from 'react-native';
import { useAppearanceStore } from '../../appearance/appearanceStore';
import { openChangePassword, openSettings } from '../../navigation/appNavigation';
import { LIGHT_PALETTE } from '../../theme/palettes';
import { SettingsButton } from '../SettingsButton';
import { SettingsList } from '../SettingsList';

jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(),
  setItemAsync: jest.fn(),
  deleteItemAsync: jest.fn(),
}));
jest.mock('../../navigation/appNavigation', () => ({ openSettings: jest.fn(), openChangePassword: jest.fn() }));

describe('the settings', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useAppearanceStore.setState({ preference: 'light' });
  });

  describe('SettingsButton', () => {
    it('opens the settings from the header', () => {
      render(<SettingsButton />);

      fireEvent.press(screen.getByRole('button', { name: 'Ajustes' }));

      expect(openSettings).toHaveBeenCalledTimes(1);
    });

    it('writes its label in the color of the text of the band', () => {
      render(<SettingsButton />);

      expect(StyleSheet.flatten(screen.getByText('Ajustes').props.style)).toMatchObject({ color: LIGHT_PALETTE.onBrand });
    });
  });

  describe('SettingsList', () => {
    it('has the appearance and the account', () => {
      render(<SettingsList />);

      expect(screen.getByText('Apariencia')).toBeTruthy();
      expect(screen.getByLabelText('Apariencia: Sistema')).toBeTruthy();
      expect(screen.getByLabelText('Apariencia: Claro')).toBeTruthy();
      expect(screen.getByLabelText('Apariencia: Oscuro')).toBeTruthy();
      expect(screen.getByText('Cuenta')).toBeTruthy();
    });

    it('changes the theme from here, and shows the choice in use', () => {
      render(<SettingsList />);
      expect(screen.getByLabelText('Apariencia: Claro').props.accessibilityState).toMatchObject({ checked: true });

      fireEvent.press(screen.getByLabelText('Apariencia: Oscuro'));

      expect(useAppearanceStore.getState().preference).toBe('dark');
      expect(screen.getByLabelText('Apariencia: Oscuro').props.accessibilityState).toMatchObject({ checked: true });
    });

    it('opens the form to change the password', () => {
      render(<SettingsList />);

      fireEvent.press(screen.getByRole('button', { name: 'Cambiar contraseña' }));

      expect(openChangePassword).toHaveBeenCalledTimes(1);
    });
  });
});
