import { act, renderHook, waitFor } from '@testing-library/react-native';
import { bookingsApi } from '../../api/client';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { goBack } from '../../navigation/appNavigation';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useReasonForm } from '../useReasonForm';

jest.mock('../../api/client', () => ({
  bookingsApi: {
    cancelBooking: jest.fn(),
    revertClientAbsent: jest.fn(),
  },
}));
jest.mock('../../navigation/appNavigation');

const confirmed = parseBooking(bookingPayload);
const tentative = { ...confirmed, status: 'Tentative' } as const;

describe('useReasonForm', () => {
  const { client, Wrapper } = createQueryWrapper();

  afterEach(() => {
    client.clear();
  });

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('does not send a confirmed booking cancellation without a reason', () => {
    const { result } = renderHook(() => useReasonForm(confirmed, 'Cancel'), { wrapper: Wrapper });

    act(() => result.current.submit());

    expect(result.current.validationError).toBe('Escribe el motivo.');
    expect(bookingsApi.cancelBooking).not.toHaveBeenCalled();
  });

  it('clears the validation message as soon as the photographer types', () => {
    const { result } = renderHook(() => useReasonForm(confirmed, 'Cancel'), { wrapper: Wrapper });
    act(() => result.current.submit());

    act(() => result.current.setText('Cambió de planes'));

    expect(result.current.validationError).toBeNull();
    expect(result.current.text).toBe('Cambió de planes');
  });

  it('cancels with the trimmed reason and goes back when the server accepts it', async () => {
    jest.mocked(bookingsApi.cancelBooking).mockResolvedValue(confirmed);
    const { result } = renderHook(() => useReasonForm(confirmed, 'Cancel'), { wrapper: Wrapper });

    act(() => result.current.setText('  Cambió de planes  '));
    act(() => result.current.submit());

    await waitFor(() => expect(goBack).toHaveBeenCalledTimes(1));
    expect(bookingsApi.cancelBooking).toHaveBeenCalledWith(confirmed.id, { reason: 'Cambió de planes' });
  });

  it('cancels a tentative booking without a reason using an empty request', async () => {
    jest.mocked(bookingsApi.cancelBooking).mockResolvedValue(tentative);
    const { result } = renderHook(() => useReasonForm(tentative, 'Cancel'), { wrapper: Wrapper });

    act(() => result.current.submit());

    await waitFor(() => expect(bookingsApi.cancelBooking).toHaveBeenCalledWith(tentative.id, {}));
  });

  it('reverts the absence with the reason', async () => {
    jest.mocked(bookingsApi.revertClientAbsent).mockResolvedValue(confirmed);
    const { result } = renderHook(() => useReasonForm(confirmed, 'RevertClientAbsent'), { wrapper: Wrapper });

    act(() => result.current.setText('Lo marqué por error'));
    act(() => result.current.submit());

    await waitFor(() => expect(bookingsApi.revertClientAbsent).toHaveBeenCalledWith(confirmed.id, { reason: 'Lo marqué por error' }));
  });
});
