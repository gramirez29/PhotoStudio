import { act, renderHook, waitFor } from '@testing-library/react-native';
import { Alert, type AlertButton } from 'react-native';
import { bookingsApi } from '../../api/client';
import { ApiError } from '../../api/httpClient';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { openRejectPayment } from '../../navigation/appNavigation';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { usePaymentReview } from '../usePaymentReview';

jest.mock('../../api/client', () => ({
  bookingsApi: { verifyPayment: jest.fn(), rejectPayment: jest.fn() },
}));
jest.mock('../../navigation/appNavigation');

const booking = parseBooking({
  ...bookingPayload,
  payments: [{ ...bookingPayload.payments[0], status: 'PendingVerification', reference: 'SINPE-1' }],
});
const proof = booking.payments[0]!;

/**
 * Returns the buttons of the last confirmation dialog.
 * @returns The buttons.
 */
function dialogButtons(): readonly AlertButton[] {
  return jest.mocked(Alert.alert).mock.calls.at(-1)?.[2] ?? [];
}

describe('usePaymentReview', () => {
  const { client, Wrapper } = createQueryWrapper();

  afterEach(() => {
    client.clear();
  });

  beforeEach(() => {
    jest.clearAllMocks();
    jest.spyOn(Alert, 'alert').mockImplementation(() => undefined);
  });

  it('asks before verifying and sends nothing if the photographer goes back', () => {
    const { result } = renderHook(() => usePaymentReview(booking), { wrapper: Wrapper });

    act(() => result.current.verify(proof));
    act(() => dialogButtons().find((button) => button.style === 'cancel')?.onPress?.());

    expect(Alert.alert).toHaveBeenCalledWith('Verificar pago', expect.stringContaining('María Pérez'), expect.any(Array));
    expect(bookingsApi.verifyPayment).not.toHaveBeenCalled();
  });

  it('verifies the proof once the photographer confirms', async () => {
    jest.mocked(bookingsApi.verifyPayment).mockResolvedValue(booking);
    const { result } = renderHook(() => usePaymentReview(booking), { wrapper: Wrapper });

    act(() => result.current.verify(proof));
    act(() => dialogButtons().find((button) => button.text === 'Verificar')?.onPress?.());

    await waitFor(() => expect(bookingsApi.verifyPayment).toHaveBeenCalledWith(booking.id, proof.id));
  });

  it('explains a proof that was already resolved', async () => {
    jest.mocked(bookingsApi.verifyPayment).mockRejectedValue(new ApiError(409, 'x', 'payment.invalid_status'));
    const { result } = renderHook(() => usePaymentReview(booking), { wrapper: Wrapper });

    act(() => result.current.verify(proof));
    act(() => dialogButtons().find((button) => button.text === 'Verificar')?.onPress?.());

    await waitFor(() => expect(result.current.errorMessage).toContain('ya fue verificado o rechazado'));
  });

  it('opens the rejection form with the booking and the proof', () => {
    const { result } = renderHook(() => usePaymentReview(booking), { wrapper: Wrapper });

    result.current.reject(proof);

    expect(openRejectPayment).toHaveBeenCalledWith(booking.id, proof.id);
  });
});
