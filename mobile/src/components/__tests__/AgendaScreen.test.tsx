import { act, render, screen } from '@testing-library/react-native';
import AgendaScreen from '../../app/index';
import { restoreAgendaPreferences } from '../../agenda/agendaManager';
import { useAgendaStore } from '../../agenda/agendaStore';
import { restoreHomeScreen } from '../../home/homeManager';
import { useHomeStore } from '../../home/homeStore';
import { useAgendaRanges } from '../../hooks/useAgendaRanges';
import { replaceWithAllBookings } from '../../navigation/appNavigation';

jest.mock('../../agenda/agendaManager', () => ({ restoreAgendaPreferences: jest.fn(), changeAgendaView: jest.fn(), changeAgendaShowAll: jest.fn() }));
jest.mock('../../hooks/useAgendaRanges');
jest.mock('../../home/homeManager', () => ({ restoreHomeScreen: jest.fn() }));
jest.mock('../../navigation/appNavigation', () => ({ openBooking: jest.fn(), openNewBooking: jest.fn(), replaceWithAllBookings: jest.fn() }));

describe('AgendaScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useHomeStore.setState({ screen: 'agenda', loaded: true, redirected: false });
    jest.useFakeTimers().setSystemTime(new Date(2026, 10, 11, 10, 0));
    useAgendaStore.setState({ view: 'schedule', anchor: new Date(2026, 10, 11), visibleDate: new Date(2026, 10, 11), showAll: false });
    jest.mocked(useAgendaRanges).mockReturnValue({
      bookings: [],
      isPending: false,
      isError: false,
      isFetching: false,
      truncated: false,
      refetch: jest.fn().mockResolvedValue(undefined),
    });
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('shows the agenda and keeps the button to create a booking within reach', () => {
    render(<AgendaScreen />);

    expect(screen.getByLabelText('Agenda de reservas')).toBeTruthy();
    expect(screen.getByLabelText('Nueva reserva')).toBeTruthy();
  });

  it('applies the choices saved on the device when it opens', () => {
    render(<AgendaScreen />);

    expect(restoreAgendaPreferences).toHaveBeenCalledTimes(1);
  });

  it('asks for the bookings that are over only when the switch is on', () => {
    const { rerender } = render(<AgendaScreen />);
    expect(jest.mocked(useAgendaRanges).mock.calls.at(-1)?.[1]).toBe(false);

    useAgendaStore.setState({ showAll: true });
    rerender(<AgendaScreen />);

    expect(jest.mocked(useAgendaRanges).mock.calls.at(-1)?.[1]).toBe(true);
  });

  it('shows the list when the chosen view has no screen yet', () => {
    useAgendaStore.setState({ view: 'bogus' as never });

    render(<AgendaScreen />);

    expect(screen.getByLabelText('Agenda de reservas')).toBeTruthy();
  });

  it('draws nothing until it is known which screen the app opens on', () => {
    useHomeStore.setState({ loaded: false });
    jest.mocked(restoreHomeScreen).mockReturnValue(new Promise(() => undefined));

    render(<AgendaScreen />);

    expect(screen.queryByLabelText('Agenda de reservas')).toBeNull();
  });

  it('goes to the list of all the bookings once when that is the screen chosen', async () => {
    useHomeStore.setState({ loaded: false });
    jest.mocked(restoreHomeScreen).mockImplementation(async () => {
      useHomeStore.setState({ screen: 'bookings', loaded: true });
    });

    render(<AgendaScreen />);
    await act(async () => undefined);

    expect(replaceWithAllBookings).toHaveBeenCalledTimes(1);
    expect(useHomeStore.getState().redirected).toBe(true);
  });

  it('stays on the agenda when that is the screen chosen', async () => {
    useHomeStore.setState({ loaded: false });
    jest.mocked(restoreHomeScreen).mockImplementation(async () => {
      useHomeStore.setState({ loaded: true });
    });

    render(<AgendaScreen />);
    await act(async () => undefined);

    expect(replaceWithAllBookings).not.toHaveBeenCalled();
  });
});
