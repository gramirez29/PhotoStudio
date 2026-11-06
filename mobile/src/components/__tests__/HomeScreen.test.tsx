import { fireEvent, render, renderHook, screen } from '@testing-library/react-native';
import { Alert, BackHandler, Platform, ToastAndroid } from 'react-native';
import HomeScreen from '../../app/index';
import { env } from '../../config/env';
import { useBookingStats } from '../../hooks/useBookingStats';
import { useExitOnDoubleBack } from '../../hooks/useExitOnDoubleBack';
import { useMenuCounters } from '../../hooks/useMenuCounters';
import { openAgenda, openAllBookings, openNewBooking, openNotifications, openRefunds } from '../../navigation/appNavigation';
import { openExternalUrl } from '../../navigation/externalLinks';
import type { BookingStatsResponse } from '../../types/api/stats';
import { AppThemeProvider } from '../AppThemeProvider';

jest.mock('../../hooks/useBookingStats');
jest.mock('../../hooks/useMenuCounters');
jest.mock('../../navigation/externalLinks', () => ({ openExternalUrl: jest.fn() }));
jest.mock('../../navigation/appNavigation', () => ({
  openAgenda: jest.fn(),
  openAllBookings: jest.fn(),
  openNewBooking: jest.fn(),
  openNotifications: jest.fn(),
  openRefunds: jest.fn(),
}));
jest.mock('expo-router', () => ({
  useFocusEffect: (effect: () => void | (() => void)) => {
    // The effect runs once, as it does when the screen comes to the front.
    const React: typeof import('react') = jest.requireActual('react');
    React.useEffect(effect, [effect]);
  },
}));
jest.mock('expo-secure-store', () => ({ getItemAsync: jest.fn(), setItemAsync: jest.fn(), deleteItemAsync: jest.fn() }));

/** Counts for the tests: ten bookings in four statuses. */
const STATS: BookingStatsResponse = {
  total: 10,
  byStatus: [
    { status: 'Tentative', count: 2 },
    { status: 'Confirmed', count: 5 },
    { status: 'Completed', count: 0 },
    { status: 'Cancelled', count: 3 },
    { status: 'Expired', count: 0 },
    { status: 'ClientAbsent', count: 0 },
  ],
};

/**
 * Makes the counts answer with what a test needs.
 * @param result Parts of the query result.
 */
function statsAre(result: Partial<ReturnType<typeof useBookingStats>>): void {
  jest.mocked(useBookingStats).mockReturnValue({ data: undefined, isError: false, refetch: jest.fn(), ...result } as ReturnType<typeof useBookingStats>);
}

/** Renders the home screen inside the theme. */
function renderHome(): void {
  render(
    <AppThemeProvider>
      <HomeScreen />
    </AppThemeProvider>,
  );
}

describe('HomeScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers().setSystemTime(new Date(2026, 10, 11, 10, 0));
    jest.mocked(useMenuCounters).mockReturnValue({ unreadCount: 0, pendingRefunds: 0, needsAttention: false });
    statsAre({ data: STATS });
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('shows the total and each status that has bookings, with its count and share', () => {
    renderHome();

    expect(screen.getByLabelText(/10 citas: 2 tentativa, 5 confirmada, 3 cancelada/)).toBeTruthy();
    expect(screen.getByLabelText('Confirmada: 5, 50 %')).toBeTruthy();
    expect(screen.getByLabelText('Cancelada: 3, 30 %')).toBeTruthy();
    expect(screen.queryByLabelText(/^Vencida/)).toBeNull();
  });

  it('asks for this month at first and for the period the photographer picks', () => {
    renderHome();
    expect(jest.mocked(useBookingStats).mock.calls.at(-1)?.[0]).toEqual({
      from: new Date(2026, 10, 1).toISOString(),
      to: new Date(2026, 11, 1).toISOString(),
    });

    fireEvent.press(screen.getByLabelText('Periodo: Todo'));

    expect(jest.mocked(useBookingStats).mock.calls.at(-1)?.[0]).toBeNull();
  });

  it('says so when there are no bookings in the period', () => {
    statsAre({ data: { total: 0, byStatus: STATS.byStatus.map((entry) => ({ ...entry, count: 0 })) } });

    renderHome();

    expect(screen.getByText('No hay citas en este periodo.')).toBeTruthy();
  });

  it('shows a spinner while the counts load, and offers to try again when they cannot be loaded', () => {
    statsAre({ data: undefined });
    const { unmount } = render(
      <AppThemeProvider>
        <HomeScreen />
      </AppThemeProvider>,
    );
    expect(screen.getByLabelText('Cargando las citas')).toBeTruthy();
    unmount();

    const refetch = jest.fn();
    statsAre({ data: undefined, isError: true, refetch });
    renderHome();
    fireEvent.press(screen.getByText('Reintentar'));

    expect(refetch).toHaveBeenCalledTimes(1);
  });

  it('says nothing is waiting, or lists the notices and refunds that are', () => {
    renderHome();
    expect(screen.getByText('No tienes nada pendiente.')).toBeTruthy();
  });

  it('opens the notices and the refunds that are waiting', () => {
    jest.mocked(useMenuCounters).mockReturnValue({ unreadCount: 2, pendingRefunds: 1, needsAttention: true });

    renderHome();
    fireEvent.press(screen.getByText('Avisos sin leer'));
    fireEvent.press(screen.getByText('Reembolsos pendientes'));

    expect(openNotifications).toHaveBeenCalledTimes(1);
    expect(openRefunds).toHaveBeenCalledTimes(1);
    expect(screen.queryByText('No tienes nada pendiente.')).toBeNull();
  });

  it('goes on to the agenda, all the bookings and a new booking', () => {
    renderHome();

    fireEvent.press(screen.getByLabelText('Agenda'));
    fireEvent.press(screen.getByLabelText('Todas las reservas'));
    fireEvent.press(screen.getByLabelText('Nueva reserva'));

    expect(openAgenda).toHaveBeenCalledTimes(1);
    expect(openAllBookings).toHaveBeenCalledTimes(1);
    expect(openNewBooking).toHaveBeenCalledTimes(1);
  });

  it('hides the link to the web area when the app does not know its address', () => {
    renderHome();

    expect(screen.queryByLabelText('Abrir la web del fotógrafo')).toBeNull();
  });

  describe('with the address of the web area', () => {
    const original = env.studioUrl;

    beforeEach(() => {
      (env as { studioUrl: string | null }).studioUrl = 'https://photosensestudio.com/studio';
    });

    afterEach(() => {
      (env as { studioUrl: string | null }).studioUrl = original;
    });

    it('opens it in the browser of the phone', () => {
      jest.mocked(openExternalUrl).mockResolvedValue(true);
      renderHome();

      fireEvent.press(screen.getByLabelText('Abrir la web del fotógrafo'));

      expect(openExternalUrl).toHaveBeenCalledWith('https://photosensestudio.com/studio');
    });

    it('tells the photographer when the phone could not open it', async () => {
      jest.mocked(openExternalUrl).mockResolvedValue(false);
      const alert = jest.spyOn(Alert, 'alert').mockImplementation(() => undefined);
      renderHome();

      fireEvent.press(screen.getByLabelText('Abrir la web del fotógrafo'));
      await Promise.resolve();
      await Promise.resolve();

      expect(alert).toHaveBeenCalledWith('No se pudo abrir el enlace', expect.any(String));
    });
  });
});

describe('useExitOnDoubleBack', () => {
  const originalOs = Platform.OS;
  let handler: (() => boolean | null | undefined) | undefined;
  const remove = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    handler = undefined;
    jest.spyOn(BackHandler, 'addEventListener').mockImplementation((_event, callback) => {
      handler = () => callback({} as never);
      return { remove };
    });
    jest.spyOn(ToastAndroid, 'show').mockImplementation(() => undefined);
    jest.useFakeTimers().setSystemTime(new Date(2026, 10, 11, 10, 0));
    Platform.OS = 'android';
  });

  afterEach(() => {
    Platform.OS = originalOs;
    jest.useRealTimers();
  });

  it('keeps the app open and warns on the first press, and leaves on a second one within two seconds', () => {
    renderHook(() => useExitOnDoubleBack());

    expect(handler?.()).toBe(true);
    expect(ToastAndroid.show).toHaveBeenCalledWith('Presiona de nuevo para salir', ToastAndroid.SHORT);

    jest.setSystemTime(new Date(2026, 10, 11, 10, 0, 1));
    expect(handler?.()).toBe(false);
  });

  it('warns again when the second press comes too late', () => {
    renderHook(() => useExitOnDoubleBack());
    handler?.();

    jest.setSystemTime(new Date(2026, 10, 11, 10, 0, 5));

    expect(handler?.()).toBe(true);
    expect(ToastAndroid.show).toHaveBeenCalledTimes(2);
  });

  it('stops listening when the screen goes away, and does nothing outside Android', () => {
    const { unmount } = renderHook(() => useExitOnDoubleBack());
    unmount();
    expect(remove).toHaveBeenCalledTimes(1);

    Platform.OS = 'ios';
    handler = undefined;
    renderHook(() => useExitOnDoubleBack());
    expect(handler).toBeUndefined();
  });
});
