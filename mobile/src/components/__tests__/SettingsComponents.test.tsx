import { fireEvent, render, screen } from '@testing-library/react-native';
import { useAppearanceStore } from '../../appearance/appearanceStore';
import { useApiHealth } from '../../hooks/useApiHealth';
import { useSessionStore } from '../../session/sessionStore';
import { openChangePassword } from '../../navigation/appNavigation';
import { SettingsList } from '../SettingsList';

jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(),
  setItemAsync: jest.fn(),
  deleteItemAsync: jest.fn(),
}));
jest.mock('../../navigation/appNavigation', () => ({ openChangePassword: jest.fn() }));
jest.mock('../../hooks/useApiHealth');

/**
 * Makes the module of the maintenance button a stand-in that shows a text, so the test sees where it goes without its hooks.
 * @returns The module.
 */
function mockMaintenance(): Record<string, () => unknown> {
  // The factory of jest.mock cannot use what is imported above, so what it needs is loaded inside.
  const React: typeof import('react') = jest.requireActual('react');
  const { Text }: typeof import('react-native') = jest.requireActual('react-native');
  return { MaintenanceButton: () => React.createElement(Text, null, 'liberar reservas vencidas') };
}

jest.mock('../MaintenanceButton', () => mockMaintenance());

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
    useSessionStore.setState({ session: null });
    serverIs({ isError: false, data: true });
  });

  describe('SettingsList', () => {
    it('shows whether the server is connected, here and not on the home screen', () => {
      render(<SettingsList />);

      expect(screen.getByText('Servidor')).toBeTruthy();
      expect(screen.getByText('Servidor conectado')).toBeTruthy();
    });

    it('has no maintenance for an ordinary photographer', () => {
      render(<SettingsList />);

      expect(screen.queryByText('Mantenimiento')).toBeNull();
      expect(screen.queryByText('liberar reservas vencidas')).toBeNull();
    });

    it('has the maintenance pass, with its title, for a system operator', () => {
      useSessionStore.setState({
        session: {
          accessToken: 't',
          accessTokenExpiresAt: 1,
          photographerId: 'p',
          username: 'u',
          email: 'u@example.com',
          name: 'n',
          isSystemOperator: true,
          emailVerified: true,
        },
      });

      render(<SettingsList />);

      expect(screen.getByText('Mantenimiento')).toBeTruthy();
      expect(screen.getByText('liberar reservas vencidas')).toBeTruthy();
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
