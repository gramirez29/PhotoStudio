import { render, screen } from '@testing-library/react-native';
import HomeScreen from '../../app/index';
import { useBookings } from '../../hooks/useBookings';
import { useSessionStore } from '../../session/sessionStore';

jest.mock('../../hooks/useBookings');
jest.mock('../../navigation/appNavigation', () => ({ openBooking: jest.fn(), openNewBooking: jest.fn() }));

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

  it('is only the list of bookings: notices, refunds, settings and signing out are in the side menu', () => {
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

    expect(screen.queryByText(/Avisos/)).toBeNull();
    expect(screen.queryByText(/Reembolsos pendientes/)).toBeNull();
    expect(screen.queryByText('Liberar reservas vencidas')).toBeNull();
    expect(screen.queryByText(/Servidor/)).toBeNull();
    expect(screen.queryByText('Apariencia')).toBeNull();
    expect(screen.queryByText('Cambiar contraseña')).toBeNull();
  });

  it('offers to retry when the bookings could not be loaded', () => {
    bookingsAre({ isError: true, data: undefined });

    render(<HomeScreen />);

    expect(screen.getByText(/No se pudieron cargar las reservas/)).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeTruthy();
  });
});
