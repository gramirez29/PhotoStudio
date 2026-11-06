import { renderHook, waitFor } from '@testing-library/react-native';
import { bookingsApi } from '../../api/client';
import { parseCalendar } from '../../api/bookingsApi';
import { bookingSummaryPayload } from '../../api/__fixtures__/testResponses';
import { useSessionStore } from '../../session/sessionStore';
import { neighborRanges, toQueryRange } from '../../utils/agenda/ranges';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { agendaQueryKey, useAgendaRange } from '../useAgendaRange';
import { usePrefetchAgenda } from '../usePrefetchAgenda';
import { bookingsQueryKey } from '../useBookings';

jest.mock('../../api/client', () => ({ bookingsApi: { getCalendar: jest.fn() } }));

const PHOTOGRAPHER = '0199a1b2-0000-7000-8000-000000000002';
const RANGE = { from: new Date(2026, 10, 8), to: new Date(2026, 10, 15) };
const calendar = parseCalendar({ items: [bookingSummaryPayload], truncated: false });

/**
 * Signs a photographer in the store, as the app does once the session is ready.
 * @param photographerId Identifier of the photographer.
 */
function signIn(photographerId: string): void {
  useSessionStore.getState().setSignedIn({
    accessToken: 'access',
    accessTokenExpiresAt: Date.now() + 900_000,
    photographerId,
    username: 'ana',
    email: 'ana@example.com',
    name: 'Ana Pérez',
    isSystemOperator: false,
    emailVerified: true,
  });
}

describe('agendaQueryKey', () => {
  it('lives under the key of the bookings of the photographer, so whatever refreshes the bookings refreshes the agenda', () => {
    const key = agendaQueryKey(PHOTOGRAPHER, false, toQueryRange(RANGE));

    expect(key.slice(0, 2)).toEqual([...bookingsQueryKey(PHOTOGRAPHER)]);
    expect(key[2]).toBe('agenda');
  });

  it('tells apart the range, the switch and the account', () => {
    const range = toQueryRange(RANGE);
    const keys = [
      agendaQueryKey(PHOTOGRAPHER, false, range),
      agendaQueryKey(PHOTOGRAPHER, true, range),
      agendaQueryKey('other', false, range),
      agendaQueryKey(PHOTOGRAPHER, false, toQueryRange({ from: RANGE.from, to: new Date(2026, 10, 16) })),
    ].map((key) => JSON.stringify(key));

    expect(new Set(keys).size).toBe(4);
  });
});

describe('useAgendaRange', () => {
  const { client, Wrapper } = createQueryWrapper();

  beforeEach(() => {
    jest.clearAllMocks();
    signIn(PHOTOGRAPHER);
  });

  afterEach(() => {
    client.clear();
    useSessionStore.getState().setSignedOut();
  });

  it('asks the backend for the range, in ISO instants, and returns the bookings', async () => {
    jest.mocked(bookingsApi.getCalendar).mockResolvedValue(calendar);

    const { result } = renderHook(() => useAgendaRange(RANGE, false), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.data).toEqual(calendar));
    expect(bookingsApi.getCalendar).toHaveBeenCalledWith(toQueryRange(RANGE), false, expect.anything());
  });

  it('asks for the bookings that are over when the switch is on', async () => {
    jest.mocked(bookingsApi.getCalendar).mockResolvedValue(calendar);

    renderHook(() => useAgendaRange(RANGE, true), { wrapper: Wrapper });

    await waitFor(() => expect(bookingsApi.getCalendar).toHaveBeenCalledWith(toQueryRange(RANGE), true, expect.anything()));
  });

  it('does not ask while nobody is signed in', () => {
    useSessionStore.getState().setSignedOut();

    renderHook(() => useAgendaRange(RANGE, false), { wrapper: Wrapper });

    expect(bookingsApi.getCalendar).not.toHaveBeenCalled();
  });

  it('reports an error without crashing', async () => {
    jest.mocked(bookingsApi.getCalendar).mockRejectedValue(new Error('offline'));

    const { result } = renderHook(() => useAgendaRange(RANGE, false), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });

  it('is refreshed when the bookings of the photographer are, so a booking just created shows up', async () => {
    jest.mocked(bookingsApi.getCalendar).mockResolvedValue(calendar);
    const { result } = renderHook(() => useAgendaRange(RANGE, false), { wrapper: Wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(bookingsApi.getCalendar).toHaveBeenCalledTimes(1);

    await client.invalidateQueries({ queryKey: bookingsQueryKey(PHOTOGRAPHER) });

    await waitFor(() => expect(bookingsApi.getCalendar).toHaveBeenCalledTimes(2));
  });
});

describe('usePrefetchAgenda', () => {
  const { client, Wrapper } = createQueryWrapper();

  beforeEach(() => {
    jest.clearAllMocks();
    signIn(PHOTOGRAPHER);
    jest.mocked(bookingsApi.getCalendar).mockResolvedValue(calendar);
  });

  afterEach(() => {
    client.clear();
    useSessionStore.getState().setSignedOut();
  });

  it('loads the previous and the next period ahead of time', async () => {
    const anchor = new Date(2026, 10, 11);

    renderHook(() => usePrefetchAgenda('week', anchor, false), { wrapper: Wrapper });

    const [previous, next] = neighborRanges('week', anchor);
    await waitFor(() => expect(bookingsApi.getCalendar).toHaveBeenCalledTimes(2));
    expect(bookingsApi.getCalendar).toHaveBeenCalledWith(toQueryRange(previous), false, expect.anything());
    expect(bookingsApi.getCalendar).toHaveBeenCalledWith(toQueryRange(next), false, expect.anything());
  });

  it('does nothing while nobody is signed in', () => {
    useSessionStore.getState().setSignedOut();

    renderHook(() => usePrefetchAgenda('week', new Date(2026, 10, 11), false), { wrapper: Wrapper });

    expect(bookingsApi.getCalendar).not.toHaveBeenCalled();
  });

  it('survives a failing prefetch', async () => {
    jest.mocked(bookingsApi.getCalendar).mockRejectedValue(new Error('offline'));

    renderHook(() => usePrefetchAgenda('month', new Date(2026, 10, 11), false), { wrapper: Wrapper });

    await waitFor(() => expect(bookingsApi.getCalendar).toHaveBeenCalledTimes(2));
  });
});
