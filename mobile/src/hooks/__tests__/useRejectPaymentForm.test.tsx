import { act, renderHook, waitFor } from '@testing-library/react-native';
import { bookingsApi } from '../../api/client';
import { ApiError } from '../../api/httpClient';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { goBack } from '../../navigation/appNavigation';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useRejectPaymentForm } from '../useRejectPaymentForm';

jest.mock('../../api/client', () => ({ bookingsApi: { rejectPayment: jest.fn() } }));
jest.mock('../../navigation/appNavigation');

const booking = parseBooking(bookingPayload);

describe('useRejectPaymentForm', () => {
  const { client, Wrapper } = createQueryWrapper();

  afterEach(() => {
    client.clear();
  });

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('asks for the reason and sends nothing while it is empty', () => {
    const { result } = renderHook(() => useRejectPaymentForm(booking.id, 'pay-1'), { wrapper: Wrapper });

    act(() => result.current.submit());

    expect(result.current.validationError).toBe('Escribe el motivo.');
    expect(bookingsApi.rejectPayment).not.toHaveBeenCalled();
  });

  it('clears the message when the reason is edited', () => {
    const { result } = renderHook(() => useRejectPaymentForm(booking.id, 'pay-1'), { wrapper: Wrapper });
    act(() => result.current.submit());

    act(() => result.current.setText('No aparece'));

    expect(result.current.validationError).toBeNull();
  });

  it('rejects the proof with the trimmed reason and goes back', async () => {
    jest.mocked(bookingsApi.rejectPayment).mockResolvedValue(booking);
    const { result } = renderHook(() => useRejectPaymentForm(booking.id, 'pay-1'), { wrapper: Wrapper });
    act(() => result.current.setText('  No aparece en el banco '));

    act(() => result.current.submit());

    await waitFor(() => expect(goBack).toHaveBeenCalledTimes(1));
    expect(bookingsApi.rejectPayment).toHaveBeenCalledWith(booking.id, 'pay-1', { reason: 'No aparece en el banco' });
  });

  it('stays on the form and explains the failure', async () => {
    jest.mocked(bookingsApi.rejectPayment).mockRejectedValue(new ApiError(409, 'x', 'payment.invalid_status'));
    const { result } = renderHook(() => useRejectPaymentForm(booking.id, 'pay-1'), { wrapper: Wrapper });
    act(() => result.current.setText('Motivo'));

    act(() => result.current.submit());

    await waitFor(() => expect(result.current.submitError).toContain('ya fue verificado o rechazado'));
    expect(goBack).not.toHaveBeenCalled();
  });
});
