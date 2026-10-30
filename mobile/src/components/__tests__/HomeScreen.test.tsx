import { render, screen } from '@testing-library/react-native';
import HomeScreen from '../../app/index';
import { useBookings } from '../../hooks/useBookings';
import { useSessionStore } from '../../session/sessionStore';

jest.mock('../../hooks/useBookings');
jest.mock('../../navigation/appNavigation', () => ({ openBooking: jest.fn(), openNewBooking: jest.fn() }));
jest.mock('react-native-safe-area-context', () => ({
  ...jest.requireActual('react-native-safe-area-context'),
  useSafeAreaInsets: () => ({ top: 0, bottom: 0, left: 0, right: 0 }),
}));
/**
 * Makes the module of a shortcut a stand-in that shows a text, so the test sees where the shortcut goes without its hooks.
 * @param name Name the module exports.
 * @param text Text the stand-in shows.
 * @returns The module.
 */
function mockStandIn(name: string, text: string): Record<string, () => unknown> {
  // The factory of jest.mock cannot use what is imported above, so what it needs is loaded inside.
  const React: typeof import('react') = jest.requireActual('react');
  const { Text }: typeof import('react-native') = jest.requireActual('react-native');
  return { [name]: () => React.createElement(Text, null, text) };
}

jest.mock('../ApiStatus', () => mockStandIn('ApiStatus', 'estado del servidor'));
jest.mock('../NotificationsButton', () => mockStandIn('NotificationsButton', 'botón de avisos'));
jest.mock('../RefundsButton', () => mockStandIn('RefundsButton', 'botón de reembolsos'));
jest.mock('../MaintenanceButton', () => mockStandIn('MaintenanceButton', 'mantenimiento'));

/**
 * Makes the list of bookings answer with what the test needs.
 * @param state Parts of the query to replace.
 */
function bookingsAre(state: Partial<ReturnType<typeof useBookings>>): void {
  jest.mocked(useBookings).mockReturnValue({
    data: [],
    isPending: false,
    isError: false,
    isRefetching: false,
    refetch: jest.fn(),
    ...state,
  } as unknown as ReturnType<typeof useBookings>);
}

describe('HomeScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useSessionStore.setState({ session: null });
  });

  it('keeps the button to create a booking within reach, as a floating button, in every state of the list', () => {
    bookingsAre({ isPending: true, data: undefined });
    const { rerender } = render(<HomeScreen />);
    expect(screen.getByLabelText('Nueva reserva')).toBeTruthy();

    bookingsAre({ isError: true, data: undefined });
    rerender(<HomeScreen />);
    expect(screen.getByLabelText('Nueva reserva')).toBeTruthy();

    bookingsAre({ data: [] });
    rerender(<HomeScreen />);
    expect(screen.getByLabelText('Nueva reserva')).toBeTruthy();
  });

  it('says there are no bookings when there are none', () => {
    bookingsAre({ data: [] });

    render(<HomeScreen />);

    expect(screen.getByText('Todavía no tienes reservas.')).toBeTruthy();
    expect(screen.getByText('Mis reservas')).toBeTruthy();
  });

  it('shows the shortcuts above the list', () => {
    bookingsAre({ data: [] });

    render(<HomeScreen />);

    expect(screen.getByText('botón de avisos')).toBeTruthy();
    expect(screen.getByText('botón de reembolsos')).toBeTruthy();
    expect(screen.queryByText('mantenimiento')).toBeNull();
  });

  it('does not carry the settings: the appearance and the password are in their own screen', () => {
    bookingsAre({ data: [] });

    render(<HomeScreen />);

    expect(screen.queryByText('Apariencia')).toBeNull();
    expect(screen.queryByLabelText('Apariencia: Oscuro')).toBeNull();
    expect(screen.queryByText('Cambiar contraseña')).toBeNull();
  });

  it('shows the maintenance shortcut only to a system operator', () => {
    bookingsAre({ data: [] });
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

    render(<HomeScreen />);

    expect(screen.getByText('mantenimiento')).toBeTruthy();
  });

  it('offers to retry when the bookings could not be loaded', () => {
    bookingsAre({ isError: true, data: undefined });

    render(<HomeScreen />);

    expect(screen.getByText(/No se pudieron cargar las reservas/)).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeTruthy();
  });
});
