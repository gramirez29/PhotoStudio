import { fireEvent, render, screen } from '@testing-library/react-native';
import { useAgendaStore } from '../../agenda/agendaStore';
import { useAgendaRanges } from '../../hooks/useAgendaRanges';
import { openBooking } from '../../navigation/appNavigation';
import type { AgendaRangesState } from '../../types/hooks/useAgendaRanges.types';
import { novemberBooking } from '../../utils/agenda/__fixtures__/agendaFixtures';
import { TimeGridView } from '../TimeGridView';

jest.mock('../../hooks/useAgendaRanges');
jest.mock('../../navigation/appNavigation', () => ({ openBooking: jest.fn() }));

/** The day the grid opens on and today, in every test: Wednesday the 11th of November 2026. */
const TODAY = new Date(2026, 10, 11);

/**
 * Makes the data of the grid answer with what a test needs.
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

/**
 * Renders the grid and gives it a width, as the phone does once it has laid it out.
 * @param view The view to render.
 */
function renderGrid(view: 'day' | 'threeDays' | 'week'): void {
  render(<TimeGridView view={view} anchor={TODAY} includeEnded={false} />);
  fireEvent(screen.getByTestId('time-grid'), 'layout', { nativeEvent: { layout: { width: 400, height: 800, x: 0, y: 0 } } });
}

describe('TimeGridView', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers().setSystemTime(new Date(2026, 10, 11, 10, 0));
    useAgendaStore.setState({ anchor: TODAY, visibleDate: TODAY });
    rangesAre({});
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('shows the seven days of the week, from Sunday, with today marked', () => {
    renderGrid('week');

    for (const day of ['8', '9', '10', '11', '12', '13', '14']) {
      expect(screen.getAllByText(day).length).toBeGreaterThan(0);
    }
    expect(screen.getAllByText('dom').length).toBeGreaterThan(0);
  });

  it('shows one day, or three, when it is asked for them', () => {
    renderGrid('day');
    expect(screen.getAllByText('mié')).toHaveLength(1);
    expect(screen.queryByText('jue')).toBeNull();
  });

  it('shows three days from the one it opens on', () => {
    renderGrid('threeDays');
    expect(screen.getAllByText('mié')).toHaveLength(1);
    expect(screen.getAllByText('jue')).toHaveLength(1);
    expect(screen.getAllByText('vie')).toHaveLength(1);
  });

  it('draws a booking as a block that opens it when touched', () => {
    rangesAre({ bookings: [novemberBooking('a', 11, 9, 10)] });

    renderGrid('day');
    fireEvent.press(screen.getByLabelText(/Reserva de Client a/));

    expect(openBooking).toHaveBeenCalledWith('a');
  });

  it('marks the current time on today', () => {
    renderGrid('week');

    expect(screen.getByLabelText('Hora actual')).toBeTruthy();
  });

  it('asks for the page and the ones next to it', () => {
    renderGrid('week');

    const ranges = jest.mocked(useAgendaRanges).mock.calls.at(-1)?.[0] ?? [];
    expect(ranges).toHaveLength(3);
    expect(ranges[1]?.from.getDate()).toBe(8);
  });

  it('tells how it failed and lets the photographer try again', () => {
    rangesAre({ isError: true });

    render(<TimeGridView view="week" anchor={TODAY} includeEnded={false} />);

    expect(screen.getByText(/No se pudo cargar la agenda/)).toBeTruthy();
  });
});
