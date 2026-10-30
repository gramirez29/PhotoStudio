import { fireEvent, render, screen } from '@testing-library/react-native';
import HomeScreen from '../../app/index';
import { useBookings } from '../../hooks/useBookings';
import { useSessionStore } from '../../session/sessionStore';

jest.mock('../../hooks/useBookings');
jest.mock('../../navigation/appNavigation', () => ({ openBooking: jest.fn(), openNewBooking: jest.fn() }));

/**
 * Builds the pages of bookings a query holds.
 * @param names Client names, one booking each, all in a single page.
 * @returns The query data.
 */
function pagesOf(...names: string[]): unknown {
  const items = names.map((clientName, index) => ({
    id: `b${index}`,
    clientName,
    packageName: 'Retrato Familiar',
    status: 'Tentative',
    sessionStart: '2026-10-17T21:00:00+00:00',
    sessionEnd: '2026-10-17T23:00:00+00:00',
    packagePrice: { amount: 100000, currency: 'CRC' },
    balance: { amount: 100000, currency: 'CRC' },
  }));
  return { pages: [{ items, nextCursor: null }], pageParams: [null] };
}

/**
 * Makes the list of bookings answer with what the test needs.
 * @param state Parts of the query to replace.
 */
function bookingsAre(state: Partial<ReturnType<typeof useBookings>>): void {
  jest.mocked(useBookings).mockReturnValue({
    data: pagesOf(),
    hasNextPage: false,
    isFetchingNextPage: false,
    isFetchNextPageError: false,
    fetchNextPage: jest.fn(),
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

    bookingsAre({ data: pagesOf() as never });
    rerender(<HomeScreen />);
    expect(screen.getByLabelText('Nueva reserva')).toBeTruthy();
  });

  it('says there are no bookings when there are none', () => {
    bookingsAre({ data: pagesOf() as never });

    render(<HomeScreen />);

    expect(screen.getByText('Todavía no tienes reservas.')).toBeTruthy();
    expect(screen.getByText('Mis reservas')).toBeTruthy();
  });

  it('is only the list of bookings: notices, refunds, settings and signing out are in the side menu', () => {
    bookingsAre({ data: pagesOf() as never });
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

  it('shows the bookings of every page loaded, and the button to see more only while there is another page', () => {
    const fetchNextPage = jest.fn();
    bookingsAre({ data: pagesOf('Ana Vargas', 'Luis Mora') as never, hasNextPage: true, fetchNextPage });

    const { rerender } = render(<HomeScreen />);

    expect(screen.getByText('Ana Vargas')).toBeTruthy();
    expect(screen.getByText('Luis Mora')).toBeTruthy();
    fireEvent.press(screen.getByText('Ver más'));
    expect(fetchNextPage).toHaveBeenCalledTimes(1);

    bookingsAre({ data: pagesOf('Ana Vargas') as never, hasNextPage: false });
    rerender(<HomeScreen />);
    expect(screen.queryByText('Ver más')).toBeNull();
  });

  it('says it is loading the next page while it comes', () => {
    bookingsAre({ data: pagesOf('Ana Vargas') as never, hasNextPage: true, isFetchingNextPage: true });

    render(<HomeScreen />);

    expect(screen.getByText('Cargando…')).toBeTruthy();
  });

  it('keeps what is shown and offers to try again when the next page fails', () => {
    bookingsAre({ data: pagesOf('Ana Vargas') as never, hasNextPage: true, isFetchNextPageError: true });

    render(<HomeScreen />);

    expect(screen.getByText('Ana Vargas')).toBeTruthy();
    expect(screen.getByText('Reintentar')).toBeTruthy();
  });

  it('offers to retry when the bookings could not be loaded', () => {
    bookingsAre({ isError: true, data: undefined });

    render(<HomeScreen />);

    expect(screen.getByText(/No se pudieron cargar las reservas/)).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeTruthy();
  });
});
