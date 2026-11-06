import { fireEvent, render, screen } from '@testing-library/react-native';
import { changeAgendaView } from '../../agenda/agendaManager';
import { useAgendaStore } from '../../agenda/agendaStore';
import { useAgendaRanges } from '../../hooks/useAgendaRanges';
import type { AgendaRangesState } from '../../types/hooks/useAgendaRanges.types';
import { novemberBooking } from '../../utils/agenda/__fixtures__/agendaFixtures';
import { MiniMonthPicker } from '../MiniMonthPicker';
import { MonthView } from '../MonthView';

jest.mock('../../hooks/useAgendaRanges');
jest.mock('../../agenda/agendaManager', () => ({ changeAgendaView: jest.fn(), changeAgendaShowAll: jest.fn(), restoreAgendaPreferences: jest.fn() }));

/** The day the views open on and today, in every test: Wednesday the 11th of November 2026. */
const TODAY = new Date(2026, 10, 11);

/**
 * Makes the data of the view answer with what a test needs.
 * @param state Parts of the state to replace.
 */
function rangesAre(state: Partial<AgendaRangesState>): void {
  jest.mocked(useAgendaRanges).mockReturnValue({
    bookings: [],
    isPending: false,
    isError: false,
    isFetching: false,
    truncated: false,
    refetch: jest.fn().mockResolvedValue(undefined),
    ...state,
  });
}

/** Renders the month view and gives it a size, as the phone does once it has laid it out. */
function renderMonth(): void {
  render(<MonthView anchor={TODAY} includeEnded={false} />);
  fireEvent(screen.getByTestId('month-view'), 'layout', { nativeEvent: { layout: { width: 350, height: 600, x: 0, y: 0 } } });
}

describe('MonthView', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers().setSystemTime(new Date(2026, 10, 11, 10, 0));
    useAgendaStore.setState({ anchor: TODAY, visibleDate: TODAY });
    rangesAre({});
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('draws the weeks of the month, from Sunday, with the days of the neighbor months', () => {
    renderMonth();

    expect(screen.getByLabelText(/(^|[^0-9])1 de noviembre.*sin reservas/)).toBeTruthy();
    expect(screen.getAllByText('dom').length).toBeGreaterThan(0);
    expect(screen.getByLabelText(/hoy/)).toBeTruthy();
  });

  it('shows the bookings of a day as bars and counts them for the screen reader', () => {
    rangesAre({ bookings: [novemberBooking('a', 11, 9, 10), novemberBooking('b', 11, 14, 15)] });

    renderMonth();

    expect(screen.getByLabelText(/hoy, 2 reservas/)).toBeTruthy();
    expect(screen.getByText('Client a')).toBeTruthy();
  });

  it('opens the day view on the day that is touched', () => {
    renderMonth();

    fireEvent.press(screen.getByLabelText(/(^|[^0-9])20 de noviembre.*sin reservas/));

    expect(useAgendaStore.getState().visibleDate.getDate()).toBe(20);
    expect(changeAgendaView).toHaveBeenCalledWith('day');
  });

  it('asks for the month and the ones next to it', () => {
    renderMonth();

    const ranges = jest.mocked(useAgendaRanges).mock.calls.at(-1)?.[0] ?? [];
    expect(ranges).toHaveLength(3);
  });
});

describe('MiniMonthPicker', () => {
  beforeEach(() => {
    jest.useFakeTimers().setSystemTime(new Date(2026, 10, 11, 10, 0));
    useAgendaStore.setState({ anchor: TODAY, visibleDate: TODAY });
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('moves the agenda to the day that is chosen and closes', () => {
    const onClose = jest.fn();
    render(<MiniMonthPicker visible onClose={onClose} />);

    fireEvent.press(screen.getByLabelText('25 de noviembre'));

    expect(useAgendaStore.getState().anchor.getDate()).toBe(25);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('goes to the next and the previous month', () => {
    render(<MiniMonthPicker visible onClose={jest.fn()} />);

    fireEvent.press(screen.getByLabelText('Mes siguiente'));
    expect(screen.getByText('Diciembre')).toBeTruthy();

    fireEvent.press(screen.getByLabelText('Mes anterior'));
    fireEvent.press(screen.getByLabelText('Mes anterior'));
    expect(screen.getByText('Octubre')).toBeTruthy();
  });
});
