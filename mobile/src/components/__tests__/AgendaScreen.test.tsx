import { render, screen } from '@testing-library/react-native';
import AgendaScreen from '../../app/index';
import { restoreAgendaPreferences } from '../../agenda/agendaManager';
import { useAgendaStore } from '../../agenda/agendaStore';
import { useAgendaRanges } from '../../hooks/useAgendaRanges';

jest.mock('../../agenda/agendaManager', () => ({ restoreAgendaPreferences: jest.fn(), changeAgendaView: jest.fn(), changeAgendaShowAll: jest.fn() }));
jest.mock('../../hooks/useAgendaRanges');
jest.mock('../../navigation/appNavigation', () => ({ openBooking: jest.fn(), openNewBooking: jest.fn() }));

describe('AgendaScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
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
    useAgendaStore.setState({ view: 'month' });

    render(<AgendaScreen />);

    expect(screen.getByLabelText('Agenda de reservas')).toBeTruthy();
  });
});
