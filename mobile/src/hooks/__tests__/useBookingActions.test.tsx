import { act, renderHook, waitFor } from '@testing-library/react-native';
import { Alert, type AlertButton } from 'react-native';
import { bookingsApi } from '../../api/client';
import { ApiError } from '../../api/httpClient';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { openReason, openReschedule } from '../../navigation/appNavigation';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useBookingActions } from '../useBookingActions';

jest.mock('../../api/client', () => ({
  bookingsApi: {
    completeBooking: jest.fn(),
    markClientAbsent: jest.fn(),
    cancelBooking: jest.fn(),
    revertClientAbsent: jest.fn(),
  },
}));
jest.mock('../../navigation/appNavigation');

const booking = parseBooking(bookingPayload);

/**
 * Returns the buttons of the last confirmation dialog.
 * @returns The buttons.
 */
function dialogButtons(): readonly AlertButton[] {
  const buttons = jest.mocked(Alert.alert).mock.calls.at(-1)?.[2];
  return buttons ?? [];
}

describe('useBookingActions', () => {
  const { client, Wrapper } = createQueryWrapper();

  afterEach(() => {
    client.clear();
  });

  beforeEach(() => {
    jest.clearAllMocks();
    jest.spyOn(Alert, 'alert').mockImplementation(() => undefined);
  });

  it('opens the reschedule form and the reason forms without asking for confirmation', () => {
    const { result } = renderHook(() => useBookingActions(booking), { wrapper: Wrapper });

    result.current.reschedule();
    result.current.cancel();
    result.current.revertClientAbsent();

    expect(openReschedule).toHaveBeenCalledWith(booking.id);
    expect(openReason).toHaveBeenCalledWith(booking.id, 'Cancel');
    expect(openReason).toHaveBeenCalledWith(booking.id, 'RevertClientAbsent');
    expect(Alert.alert).not.toHaveBeenCalled();
  });

  it('asks before completing and sends nothing until the photographer confirms', async () => {
    jest.mocked(bookingsApi.completeBooking).mockResolvedValue(booking);
    const { result } = renderHook(() => useBookingActions(booking), { wrapper: Wrapper });

    act(() => result.current.complete());

    expect(Alert.alert).toHaveBeenCalledTimes(1);
    expect(bookingsApi.completeBooking).not.toHaveBeenCalled();

    act(() => dialogButtons().find((button) => button.text === 'Completar')?.onPress?.());

    await waitFor(() => expect(bookingsApi.completeBooking).toHaveBeenCalledWith(booking.id));
  });

  it('does not send anything when the photographer goes back from the dialog', () => {
    const { result } = renderHook(() => useBookingActions(booking), { wrapper: Wrapper });

    act(() => result.current.markClientAbsent());
    act(() => dialogButtons().find((button) => button.style === 'cancel')?.onPress?.());

    expect(bookingsApi.markClientAbsent).not.toHaveBeenCalled();
  });

  it('exposes a readable message when the action fails', async () => {
    jest.mocked(bookingsApi.markClientAbsent).mockRejectedValue(new ApiError(409, 'x', 'booking.guard_failed'));
    const { result } = renderHook(() => useBookingActions(booking), { wrapper: Wrapper });

    act(() => result.current.markClientAbsent());
    act(() => dialogButtons().find((button) => button.text === 'Marcar ausente')?.onPress?.());

    await waitFor(() => expect(result.current.errorMessage).toContain('Todavía no se cumplen'));
  });
});
