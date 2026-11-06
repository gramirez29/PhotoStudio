import { renderHook, waitFor } from '@testing-library/react-native';
import { bookingsApi } from '../../api/client';
import { parseCalendar } from '../../api/bookingsApi';
import { useSessionStore } from '../../session/sessionStore';
import { toQueryRange } from '../../utils/agenda/ranges';
import { novemberBooking } from '../../utils/agenda/__fixtures__/agendaFixtures';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useAgendaRanges } from '../useAgendaRanges';

jest.mock('../../api/client', () => ({ bookingsApi: { getCalendar: jest.fn() } }));

const FIRST = { from: new Date(2026, 10, 1), to: new Date(2026, 10, 15) };
const SECOND = { from: new Date(2026, 10, 15), to: new Date(2026, 11, 1) };

/**
 * Builds the answer of the backend for some bookings.
 * @param bookings Bookings of the range.
 * @param truncated Whether the backend left some out.
 * @returns The parsed answer.
 */
function answer(bookings: ReturnType<typeof novemberBooking>[], truncated = false): ReturnType<typeof parseCalendar> {
  return parseCalendar({ items: bookings, truncated });
}

describe('useAgendaRanges', () => {
  const { client, Wrapper } = createQueryWrapper();

  beforeEach(() => {
    jest.clearAllMocks();
    useSessionStore.getState().setSignedIn({
      accessToken: 'access',
      accessTokenExpiresAt: Date.now() + 900_000,
      photographerId: 'p1',
      username: 'ana',
      email: 'ana@example.com',
      name: 'Ana Pérez',
      isSystemOperator: false,
      emailVerified: true,
    });
  });

  afterEach(() => {
    client.clear();
    useSessionStore.getState().setSignedOut();
  });

  it('is pending until a range answers, and then has its bookings', async () => {
    jest.mocked(bookingsApi.getCalendar).mockResolvedValue(answer([novemberBooking('a', 3, 9, 10)]));

    const { result } = renderHook(() => useAgendaRanges([FIRST], false), { wrapper: Wrapper });
    expect(result.current.isPending).toBe(true);

    await waitFor(() => expect(result.current.bookings.map((item) => item.id)).toEqual(['a']));
    expect(result.current.isPending).toBe(false);
    expect(result.current.isError).toBe(false);
  });

  it('asks for every range, in ISO instants, and for the ended ones only when told to', async () => {
    jest.mocked(bookingsApi.getCalendar).mockResolvedValue(answer([]));

    renderHook(() => useAgendaRanges([FIRST, SECOND], true), { wrapper: Wrapper });

    await waitFor(() => expect(bookingsApi.getCalendar).toHaveBeenCalledTimes(2));
    expect(bookingsApi.getCalendar).toHaveBeenCalledWith(toQueryRange(FIRST), true, expect.anything());
    expect(bookingsApi.getCalendar).toHaveBeenCalledWith(toQueryRange(SECOND), true, expect.anything());
  });

  it('puts the bookings of every range together, once each and by start, even when one crosses the limit of two ranges', async () => {
    const crossing = novemberBooking('crossing', 14, 22, 23);
    jest.mocked(bookingsApi.getCalendar).mockImplementation((range) =>
      Promise.resolve(
        range.from === toQueryRange(FIRST).from
          ? answer([novemberBooking('late', 10, 9, 10), crossing])
          : answer([crossing, novemberBooking('early', 2, 9, 10)]),
      ),
    );

    const { result } = renderHook(() => useAgendaRanges([FIRST, SECOND], false), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.bookings).toHaveLength(3));
    expect(result.current.bookings.map((item) => item.id)).toEqual(['early', 'late', 'crossing']);
  });

  it('says when the backend left bookings out of some range', async () => {
    jest.mocked(bookingsApi.getCalendar).mockResolvedValue(answer([novemberBooking('a', 3, 9, 10)], true));

    const { result } = renderHook(() => useAgendaRanges([FIRST], false), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.truncated).toBe(true));
  });

  it('is an error only when nothing could be loaded: a range that fails beside one that loaded is not', async () => {
    jest.mocked(bookingsApi.getCalendar).mockImplementation((range) =>
      range.from === toQueryRange(FIRST).from ? Promise.resolve(answer([novemberBooking('a', 3, 9, 10)])) : Promise.reject(new Error('offline')),
    );

    const { result } = renderHook(() => useAgendaRanges([FIRST, SECOND], false), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.bookings).toHaveLength(1));
    await waitFor(() => expect(result.current.isFetching).toBe(false));
    expect(result.current.isError).toBe(false);
  });

  it('reports an error when every range fails', async () => {
    jest.mocked(bookingsApi.getCalendar).mockRejectedValue(new Error('offline'));

    const { result } = renderHook(() => useAgendaRanges([FIRST], false), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });

  it('does not ask while nobody is signed in', () => {
    useSessionStore.getState().setSignedOut();

    renderHook(() => useAgendaRanges([FIRST], false), { wrapper: Wrapper });

    expect(bookingsApi.getCalendar).not.toHaveBeenCalled();
  });

  it('reloads every range when asked', async () => {
    jest.mocked(bookingsApi.getCalendar).mockResolvedValue(answer([]));
    const { result } = renderHook(() => useAgendaRanges([FIRST, SECOND], false), { wrapper: Wrapper });
    await waitFor(() => expect(bookingsApi.getCalendar).toHaveBeenCalledTimes(2));

    await result.current.refetch();

    await waitFor(() => expect(bookingsApi.getCalendar).toHaveBeenCalledTimes(4));
  });
});
