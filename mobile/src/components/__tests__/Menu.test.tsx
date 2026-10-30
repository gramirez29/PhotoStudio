import { MaterialIcons } from '@expo/vector-icons';
import { act, fireEvent, render, renderHook, screen } from '@testing-library/react-native';
import { Alert, type AlertButton } from 'react-native';
import { useAppearanceStore } from '../../appearance/appearanceStore';
import { useMenuCounters } from '../../hooks/useMenuCounters';
import { useNotifications } from '../../hooks/useNotifications';
import { useRefunds } from '../../hooks/useRefunds';
import { useMenuStore } from '../../menu/menuStore';
import { openNotifications, openRefunds, openSettings } from '../../navigation/appNavigation';
import { signOutOfDevice } from '../../push/pushSignOut';
import { useSessionStore } from '../../session/sessionStore';
import { LIGHT_PALETTE } from '../../theme/palettes';
import { AppMenu } from '../AppMenu';
import { AppThemeProvider } from '../AppThemeProvider';
import { MenuButton } from '../MenuButton';

jest.mock('../../hooks/useNotifications');
jest.mock('../../hooks/useRefunds');
jest.mock('../../push/pushSignOut', () => ({ signOutOfDevice: jest.fn() }));
jest.mock('../../navigation/appNavigation', () => ({ openNotifications: jest.fn(), openRefunds: jest.fn(), openSettings: jest.fn() }));

/**
 * Makes the inbox and the list of refunds answer with the counts a test needs.
 * @param unread Notices not read yet.
 * @param pending Refunds still owed.
 */
function waiting(unread: number, pending: number): void {
  jest.mocked(useNotifications).mockReturnValue({ data: { items: [], unreadCount: unread } } as unknown as ReturnType<typeof useNotifications>);
  jest.mocked(useRefunds).mockReturnValue({ data: { items: [], pendingCount: pending } } as unknown as ReturnType<typeof useRefunds>);
}

/** Signs a photographer in, in the store. */
function signedIn(): void {
  useSessionStore.setState({
    status: 'signedIn',
    session: {
      accessToken: 't',
      accessTokenExpiresAt: 1,
      photographerId: 'p',
      username: 'guille',
      email: 'guille@example.com',
      name: 'Guillermo Ramírez',
      isSystemOperator: false,
      emailVerified: true,
    },
  });
}

/** Renders the menu inside the theme, as the app does. */
function renderMenu(): void {
  render(
    <AppThemeProvider>
      <AppMenu />
    </AppThemeProvider>,
  );
}

describe('the side menu', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.spyOn(Alert, 'alert').mockImplementation(() => undefined);
    useAppearanceStore.setState({ preference: 'light' });
    useMenuStore.setState({ open: false });
    signedIn();
    waiting(0, 0);
  });

  describe('state', () => {
    it('starts closed, opens and closes', () => {
      expect(useMenuStore.getState().open).toBe(false);

      useMenuStore.getState().openMenu();
      expect(useMenuStore.getState().open).toBe(true);

      useMenuStore.getState().closeMenu();
      expect(useMenuStore.getState().open).toBe(false);
    });
  });

  describe('useMenuCounters', () => {
    it('counts the notices and the refunds that are waiting', () => {
      waiting(3, 2);

      const { result } = renderHook(() => useMenuCounters());

      expect(result.current).toEqual({ unreadCount: 3, pendingRefunds: 2, needsAttention: true });
    });

    it('needs attention for either one alone, and for neither it does not', () => {
      waiting(1, 0);
      expect(renderHook(() => useMenuCounters()).result.current.needsAttention).toBe(true);
      waiting(0, 1);
      expect(renderHook(() => useMenuCounters()).result.current.needsAttention).toBe(true);
      waiting(0, 0);
      expect(renderHook(() => useMenuCounters()).result.current.needsAttention).toBe(false);
    });

    it('counts nothing while the queries have not answered', () => {
      jest.mocked(useNotifications).mockReturnValue({ data: undefined } as unknown as ReturnType<typeof useNotifications>);
      jest.mocked(useRefunds).mockReturnValue({ data: undefined } as unknown as ReturnType<typeof useRefunds>);

      expect(renderHook(() => useMenuCounters()).result.current).toEqual({ unreadCount: 0, pendingRefunds: 0, needsAttention: false });
    });
  });

  describe('MenuButton', () => {
    it('is the three lines, in the color of the text of the band, and opens the menu', () => {
      render(<MenuButton />);

      const icon = screen.UNSAFE_getByType(MaterialIcons);
      expect(icon.props.name).toBe('menu');
      expect(icon.props.color).toBe(LIGHT_PALETTE.onBrand);

      fireEvent.press(screen.getByRole('button', { name: 'Abrir el menú' }));

      expect(useMenuStore.getState().open).toBe(true);
    });

    it('has no dot while nothing is waiting', () => {
      render(<MenuButton />);

      expect(screen.queryByTestId('menu-attention-dot')).toBeNull();
    });

    it('carries a dot, and says so, when a notice or a refund is waiting', () => {
      waiting(0, 1);

      render(<MenuButton />);

      expect(screen.getByTestId('menu-attention-dot')).toBeTruthy();
      expect(screen.getByRole('button', { name: 'Abrir el menú, hay avisos o reembolsos pendientes' })).toBeTruthy();
    });
  });

  describe('AppMenu', () => {
    it('draws nothing while it is closed', () => {
      renderMenu();

      expect(screen.queryByText('Inicio')).toBeNull();
    });

    it('shows the studio and who is signed in, and every option, when it opens', () => {
      useMenuStore.setState({ open: true });

      renderMenu();

      expect(screen.getByText('PhotoSense Studio')).toBeTruthy();
      expect(screen.getByText('Guillermo Ramírez')).toBeTruthy();
      expect(screen.getByText('guille@example.com')).toBeTruthy();
      for (const label of ['Inicio', 'Avisos', 'Reembolsos pendientes', 'Ajustes', 'Cerrar sesión']) {
        expect(screen.getByText(label)).toBeTruthy();
      }
    });

    it('leaves the email out when the account has none', () => {
      useSessionStore.setState({ session: { ...useSessionStore.getState().session!, email: '' } });
      useMenuStore.setState({ open: true });

      renderMenu();

      expect(screen.queryByText('guille@example.com')).toBeNull();
      expect(screen.getByText('Guillermo Ramírez')).toBeTruthy();
    });

    it('draws nothing without a signed-in photographer', () => {
      useSessionStore.setState({ status: 'signedOut', session: null });
      useMenuStore.setState({ open: true });

      renderMenu();

      expect(screen.queryByText('Inicio')).toBeNull();
    });

    it('says how many notices are unread and how many refunds are owed, and says nothing when there are none', () => {
      waiting(3, 2);
      useMenuStore.setState({ open: true });
      renderMenu();

      expect(screen.getByLabelText('Avisos (3)')).toBeTruthy();
      expect(screen.getByLabelText('Reembolsos pendientes (2)')).toBeTruthy();
      expect(screen.getByText('3')).toBeTruthy();
      expect(screen.getByText('2')).toBeTruthy();
    });

    it('shows no counter for what is not waiting', () => {
      useMenuStore.setState({ open: true });

      renderMenu();

      expect(screen.getByLabelText('Avisos')).toBeTruthy();
      expect(screen.getByLabelText('Reembolsos pendientes')).toBeTruthy();
      expect(screen.queryByText('0')).toBeNull();
    });

    it.each([
      ['Avisos', openNotifications],
      ['Reembolsos pendientes', openRefunds],
      ['Ajustes', openSettings],
    ] as const)('goes to %s and closes the menu', (label, destination) => {
      useMenuStore.setState({ open: true });
      renderMenu();

      fireEvent.press(screen.getByText(label));

      expect(destination).toHaveBeenCalledTimes(1);
      expect(useMenuStore.getState().open).toBe(false);
    });

    it('only closes the menu from Inicio: the photographer is already there', () => {
      useMenuStore.setState({ open: true });
      renderMenu();

      fireEvent.press(screen.getByText('Inicio'));

      expect(useMenuStore.getState().open).toBe(false);
      expect(openNotifications).not.toHaveBeenCalled();
      expect(openRefunds).not.toHaveBeenCalled();
      expect(openSettings).not.toHaveBeenCalled();
    });

    it('closes from the dimmed screen without going anywhere', () => {
      useMenuStore.setState({ open: true });
      renderMenu();

      fireEvent.press(screen.getByLabelText('Cerrar el menú de navegación'));

      expect(useMenuStore.getState().open).toBe(false);
      expect(openSettings).not.toHaveBeenCalled();
    });

    it('asks before signing out, and signs out nothing until the photographer confirms', () => {
      useMenuStore.setState({ open: true });
      renderMenu();

      fireEvent.press(screen.getByText('Cerrar sesión'));

      expect(Alert.alert).toHaveBeenCalledTimes(1);
      expect(signOutOfDevice).not.toHaveBeenCalled();
      expect(useMenuStore.getState().open).toBe(false);
      const buttons: readonly AlertButton[] = jest.mocked(Alert.alert).mock.calls.at(-1)?.[2] ?? [];
      act(() => buttons.find((button) => button.style === 'destructive')?.onPress?.());
      expect(signOutOfDevice).toHaveBeenCalledTimes(1);
    });

    it('opens from the button of the header: the two work together', () => {
      render(
        <AppThemeProvider>
          <MenuButton />
          <AppMenu />
        </AppThemeProvider>,
      );
      expect(screen.queryByText('Inicio')).toBeNull();

      fireEvent.press(screen.getByRole('button', { name: 'Abrir el menú' }));

      expect(screen.getByText('Inicio')).toBeTruthy();
    });
  });
});
