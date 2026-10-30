import { fireEvent, render, screen } from '@testing-library/react-native';
import { MaterialIcons } from '@expo/vector-icons';
import { useAppearanceStore } from '../../appearance/appearanceStore';
import { useApiHealth } from '../../hooks/useApiHealth';
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
jest.mock('../../hooks/useApiHealth');

/**
 * Makes the readiness check of the server answer.
 * @param state Parts of the query to replace.
 */
function serverIs(state: { isError: boolean; data: boolean | undefined }): void {
  jest.mocked(useApiHealth).mockReturnValue(state as unknown as ReturnType<typeof useApiHealth>);
}

describe('the settings', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useAppearanceStore.setState({ preference: 'light' });
    serverIs({ isError: false, data: true });
  });

  describe('SettingsButton', () => {
    it('opens the settings from the header', () => {
      render(<SettingsButton />);

      fireEvent.press(screen.getByRole('button', { name: 'Ajustes' }));

      expect(openSettings).toHaveBeenCalledTimes(1);
    });

    it('is a gear, in the color of the text of the band, and has no text of its own', () => {
      render(<SettingsButton />);

      const gear = screen.UNSAFE_getByType(MaterialIcons);
      expect(gear.props.name).toBe('settings');
      expect(gear.props.color).toBe(LIGHT_PALETTE.onBrand);
      expect(screen.queryByText('Ajustes')).toBeNull();
    });

    it('is named for the screen reader, and the gear is hidden from it', () => {
      render(<SettingsButton />);

      expect(screen.getByLabelText('Ajustes')).toBeTruthy();
      expect(screen.UNSAFE_getByType(MaterialIcons).props.accessible).toBe(false);
    });
  });

  describe('SettingsList', () => {
    it('shows whether the server is connected, here and not on the home screen', () => {
      render(<SettingsList />);

      expect(screen.getByText('Servidor')).toBeTruthy();
      expect(screen.getByText('Servidor conectado')).toBeTruthy();
    });

    it('says so when the server cannot be reached', () => {
      serverIs({ isError: true, data: undefined });

      render(<SettingsList />);

      expect(screen.getByText('Sin conexión con el servidor')).toBeTruthy();
    });

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
