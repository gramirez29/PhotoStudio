import { act, renderHook, waitFor } from '@testing-library/react-native';
import { bookingsApi } from '../../api/client';
import { parseBooking } from '../../api/bookingsApi';
import { ApiError } from '../../api/httpClient';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { goBack } from '../../navigation/appNavigation';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useEditBookingForm } from '../useEditBookingForm';

jest.mock('../../api/client', () => ({ bookingsApi: { editBookingDetails: jest.fn() } }));
jest.mock('../../navigation/appNavigation');

/** A booking that still allows changing the package: nothing depends on it yet. */
const editable = parseBooking({ ...bookingPayload, clientPhone: '+50670189220', allowedActions: ['EditDetails', 'EditPackage', 'Cancel'] });
/** A booking whose package is locked: the backend does not offer `EditPackage`. */
const locked = parseBooking({ ...bookingPayload, clientPhone: '+50670189220', allowedActions: ['EditDetails', 'Cancel'] });

describe('useEditBookingForm', () => {
  const { client, Wrapper } = createQueryWrapper();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  afterEach(() => {
    client.clear();
  });

  it('starts with what the booking has now', () => {
    const { result } = renderHook(() => useEditBookingForm(editable), { wrapper: Wrapper });

    expect(result.current.values.clientName).toBe(editable.clientName);
    expect(result.current.values.clientPhone).toBe('7018-9220');
    expect(result.current.values.packageName).toBe(editable.packageName);
  });

  it('lets the package change only when the backend offers EditPackage', () => {
    const open = renderHook(() => useEditBookingForm(editable), { wrapper: Wrapper });
    const closed = renderHook(() => useEditBookingForm(locked), { wrapper: Wrapper });

    expect(open.result.current.packageEditable).toBe(true);
    expect(closed.result.current.packageEditable).toBe(false);
  });

  it('writes the phone as it is typed and forgets the message of a field that is edited', () => {
    const { result } = renderHook(() => useEditBookingForm(editable), { wrapper: Wrapper });
    act(() => result.current.setText('clientName', ' '));
    act(() => result.current.submit());
    expect(result.current.errors.clientName).toBeDefined();

    act(() => result.current.setText('clientName', 'María'));
    act(() => result.current.setPhone('70189221'));

    expect(result.current.errors.clientName).toBeUndefined();
    expect(result.current.values.clientPhone).toBe('7018-9221');
  });

  it('does not call the backend while the form is invalid', () => {
    const { result } = renderHook(() => useEditBookingForm(editable), { wrapper: Wrapper });
    act(() => result.current.setText('clientEmail', 'no-es-un-correo'));

    act(() => result.current.submit());

    expect(result.current.errors.clientEmail).toBeDefined();
    expect(bookingsApi.editBookingDetails).not.toHaveBeenCalled();
  });

  it('saves the changes and goes back when the server accepts them', async () => {
    jest.mocked(bookingsApi.editBookingDetails).mockResolvedValue(editable);
    const { result } = renderHook(() => useEditBookingForm(editable), { wrapper: Wrapper });
    act(() => {
      result.current.setText('clientEmail', 'nuevo@example.com');
      result.current.setPackage('Pre-Boda');
      result.current.setText('price', '150000');
    });

    act(() => result.current.submit());

    await waitFor(() => expect(goBack).toHaveBeenCalledTimes(1));
    expect(bookingsApi.editBookingDetails).toHaveBeenCalledWith(editable.id, {
      clientName: editable.clientName,
      clientPhone: '+50670189220',
      clientEmail: 'nuevo@example.com',
      packageName: 'Pre-Boda',
      packagePrice: 150000,
    });
  });

  it('sends the package of the booking, not the form, when it is locked', async () => {
    jest.mocked(bookingsApi.editBookingDetails).mockResolvedValue(locked);
    const { result } = renderHook(() => useEditBookingForm(locked), { wrapper: Wrapper });
    act(() => result.current.setText('price', '1'));

    act(() => result.current.submit());

    await waitFor(() => expect(goBack).toHaveBeenCalledTimes(1));
    expect(bookingsApi.editBookingDetails).toHaveBeenCalledWith(
      locked.id,
      expect.objectContaining({ packageName: locked.packageName, packagePrice: locked.packagePrice.amount }),
    );
  });

  it('says why the package could not change when the server refuses it', async () => {
    jest.mocked(bookingsApi.editBookingDetails).mockRejectedValue(new ApiError(409, 'locked', 'booking.guard_failed'));
    const { result } = renderHook(() => useEditBookingForm(editable), { wrapper: Wrapper });

    act(() => result.current.submit());

    await waitFor(() => expect(result.current.submitError).toMatch(/El paquete y el precio ya no se pueden cambiar/));
    expect(goBack).not.toHaveBeenCalled();
  });
});
