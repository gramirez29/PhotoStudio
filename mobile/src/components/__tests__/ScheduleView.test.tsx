import { act, fireEvent, render, screen } from '@testing-library/react-native';
import { useAgendaStore } from '../../agenda/agendaStore';
import { useAgendaRanges } from '../../hooks/useAgendaRanges';
import { openBooking } from '../../navigation/appNavigation';
import type { AgendaRangesState } from '../../types/hooks/useAgendaRanges.types';
import { booking, novemberBooking } from '../../utils/agenda/__fixtures__/agendaFixtures';
import { ScheduleView } from '../ScheduleView';

jest.mock('../../hooks/useAgendaRanges');
jest.mock('../../navigation/appNavigation', () => ({ openBooking: jest.fn() }));

/** The day the list opens on and today, in every test: the 11th of November 2026. */
const TODAY = new Date(2026, 10, 11);

/**
 * Makes the data of the list answer with what a test needs.
 * @param state Parts of the state to replace.
 * @returns The mock, to read the calls it received.
 */
function rangesAre(state: Partial<AgendaRangesState>): jest.MockedFunction<typeof useAgendaRanges> {
  const refetch = jest.fn().mockResolvedValue(undefined);
  return jest.mocked(useAgendaRanges).mockReturnValue({
    bookings: [],
    isPending: false,
    isError: false,
    isFetching: false,
    truncated: false,
    refetch,
    ...state,
  });
}

/**
 * Renders the list on the day the tests use.
 * @param includeEnded Whether the bookings that are over are shown.
 */
function renderList(includeEnded = false): void {
  render(<ScheduleView anchor={TODAY} includeEnded={includeEnded} />);
}

/**
 * Reads how many ranges the list asked for in its last render.
 * @returns The number of ranges.
 */
function askedRanges(): number {
  const calls = jest.mocked(useAgendaRanges).mock.calls;
  return calls[calls.length - 1]?.[0].length ?? 0;
}

describe('ScheduleView', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers().setSystemTime(new Date(2026, 10, 11, 10, 0));
    useAgendaStore.setState({ view: 'schedule', anchor: TODAY, visibleDate: TODAY, showAll: false });
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('shows a spinner while the first bookings load', () => {
    rangesAre({ isPending: true });

    renderList();

    expect(screen.getByLabelText('Cargando la agenda')).toBeTruthy();
  });

  it('shows a message with a way to retry when nothing could be loaded', () => {
    const mock = rangesAre({ isError: true });

    renderList();
    fireEvent.press(screen.getByRole('button', { name: /Reintentar/ }));

    expect(screen.getByText(/No se pudo cargar la agenda/)).toBeTruthy();
    expect(mock.mock.results[0]?.value.refetch).toHaveBeenCalled();
  });

  it('lists the days that have bookings, with the banner of each month, and always today', () => {
    rangesAre({
      bookings: [
        novemberBooking('later', 20, 9, 10),
        booking('december', new Date(2026, 11, 2, 9), new Date(2026, 11, 2, 10)),
      ],
    });

    renderList();

    expect(screen.getByText('Noviembre')).toBeTruthy();
    expect(screen.getByText('Diciembre')).toBeTruthy();
    expect(screen.getByText('Sin reservas')).toBeTruthy();
    expect(screen.getByText('Client later')).toBeTruthy();
    expect(screen.getByText('Client december')).toBeTruthy();
  });

  it('opens a booking when it is tapped', () => {
    rangesAre({ bookings: [novemberBooking('a', 11, 9, 10)] });

    renderList();
    fireEvent.press(screen.getByRole('button', { name: /Reserva de Client a/ }));

    expect(openBooking).toHaveBeenCalledWith('a');
  });

  it('asks for the 90 days from the day it opens on, in chunks of 30, and says whether the ended bookings are wanted', () => {
    const mock = rangesAre({});

    renderList(true);

    const [ranges, includeEnded] = mock.mock.calls[0] ?? [];
    expect(includeEnded).toBe(true);
    expect(ranges).toHaveLength(3);
    expect(ranges?.[0]).toEqual({ from: new Date(2026, 10, 11), to: new Date(2026, 11, 11) });
    expect(ranges?.[2]?.to).toEqual(new Date(2027, 1, 9));
  });

  it('has no links to ask for more days: it loads them by itself', () => {
    rangesAre({});

    renderList();

    expect(screen.queryByText(/Ver reservas/)).toBeNull();
  });

  it('warns when the backend left some bookings out', () => {
    rangesAre({ truncated: true });

    renderList();

    expect(screen.getByText(/Hay más reservas de las que se pueden mostrar/)).toBeTruthy();
  });

  describe('scrolling', () => {
    const nearTheEnd = { nativeEvent: { contentOffset: { y: 2500 }, layoutMeasurement: { height: 600 }, contentSize: { height: 3000 } } };

    it('does not load anything until the photographer has scrolled by hand', () => {
      rangesAre({});
      renderList();

      fireEvent.scroll(screen.getByLabelText('Agenda de reservas'), nearTheEnd);

      expect(askedRanges()).toBe(3);
    });

    it('loads the next days when the photographer scrolls near the end', () => {
      rangesAre({});
      renderList();
      const list = screen.getByLabelText('Agenda de reservas');

      fireEvent(list, 'scrollBeginDrag');
      fireEvent.scroll(list, nearTheEnd);

      expect(askedRanges()).toBe(4);
      expect(jest.mocked(useAgendaRanges).mock.calls.at(-1)?.[0][3]?.from).toEqual(new Date(2027, 1, 9));
    });

    it('loads the previous days when the photographer scrolls near the top', () => {
      rangesAre({});
      renderList();
      const list = screen.getByLabelText('Agenda de reservas');

      fireEvent(list, 'scrollBeginDrag');
      fireEvent.scroll(list, { nativeEvent: { contentOffset: { y: 100 }, layoutMeasurement: { height: 600 }, contentSize: { height: 3000 } } });

      expect(askedRanges()).toBe(4);
      expect(jest.mocked(useAgendaRanges).mock.calls.at(-1)?.[0][0]?.from).toEqual(new Date(2026, 9, 12));
    });

    it('does nothing in the middle of the list', () => {
      rangesAre({});
      renderList();
      const list = screen.getByLabelText('Agenda de reservas');

      fireEvent(list, 'scrollBeginDrag');
      fireEvent.scroll(list, { nativeEvent: { contentOffset: { y: 1200 }, layoutMeasurement: { height: 600 }, contentSize: { height: 3000 } } });

      expect(askedRanges()).toBe(3);
    });

    it('does not ask for more while something is loading', () => {
      rangesAre({ isFetching: true });
      renderList();
      const list = screen.getByLabelText('Agenda de reservas');

      fireEvent(list, 'scrollBeginDrag');
      fireEvent.scroll(list, nearTheEnd);

      expect(askedRanges()).toBe(3);
    });

    it('shows a spinner at the end while the days after the first ones load, and none otherwise', () => {
      rangesAre({});
      const { rerender } = render(<ScheduleView anchor={TODAY} includeEnded={false} />);
      const list = screen.getByLabelText('Agenda de reservas');
      expect(screen.queryByLabelText('Cargando reservas posteriores')).toBeNull();

      fireEvent(list, 'scrollBeginDrag');
      fireEvent.scroll(list, nearTheEnd);
      rangesAre({ isFetching: true });
      rerender(<ScheduleView anchor={TODAY} includeEnded={false} />);

      expect(screen.getByLabelText('Cargando reservas posteriores')).toBeTruthy();
      expect(screen.queryByLabelText('Cargando reservas anteriores')).toBeNull();

      rangesAre({ isFetching: false });
      rerender(<ScheduleView anchor={TODAY} includeEnded={false} />);
      expect(screen.queryByLabelText('Cargando reservas posteriores')).toBeNull();
    });

    it('shows a spinner at the top while the previous days load', () => {
      rangesAre({});
      const { rerender } = render(<ScheduleView anchor={TODAY} includeEnded={false} />);
      const list = screen.getByLabelText('Agenda de reservas');

      fireEvent(list, 'scrollBeginDrag');
      fireEvent.scroll(list, { nativeEvent: { contentOffset: { y: 100 }, layoutMeasurement: { height: 600 }, contentSize: { height: 3000 } } });
      rangesAre({ isFetching: true });
      rerender(<ScheduleView anchor={TODAY} includeEnded={false} />);

      expect(screen.getByLabelText('Cargando reservas anteriores')).toBeTruthy();
    });

    it('does not show a spinner for the background refresh of the days it already had', () => {
      rangesAre({ isFetching: true });

      renderList();

      expect(screen.queryByLabelText('Cargando reservas posteriores')).toBeNull();
      expect(screen.queryByLabelText('Cargando reservas anteriores')).toBeNull();
    });
  });

  it('tells the header which day is at the top as the list scrolls', () => {
    rangesAre({ bookings: [novemberBooking('a', 20, 9, 10)] });
    renderList();
    const list = screen.getByLabelText('Agenda de reservas');

    act(() => {
      list.props.onViewableItemsChanged({
        viewableItems: [{ item: { kind: 'day', key: 'day-2026-11-20', date: new Date(2026, 10, 20) }, key: 'day-2026-11-20', index: 1, isViewable: true }],
        changed: [],
      });
    });

    expect(useAgendaStore.getState().visibleDate).toEqual(new Date(2026, 10, 20));
  });

  it('reloads every range when it is pulled down', async () => {
    const mock = rangesAre({});
    renderList();

    await act(async () => {
      screen.getByLabelText('Agenda de reservas').props.onRefresh();
    });

    expect(mock.mock.results[0]?.value.refetch).toHaveBeenCalledTimes(1);
  });
});
