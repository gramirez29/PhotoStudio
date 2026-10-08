import { act, renderHook, waitFor } from '@testing-library/react-native';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { bookingsApi } from '../../api/client';
import { replaceWithBooking } from '../../navigation/appNavigation';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useNewBookingForm } from '../useNewBookingForm';

jest.mock('../../config/env', () => ({ env: { apiUrl: 'https://api.example.test' } }));
jest.mock('../../api/client', () => ({ bookingsApi: { createBooking: jest.fn() } }));
jest.mock('../../navigation/appNavigation');

describe('useNewBookingForm', () => {
  const { client, Wrapper } = createQueryWrapper();

  afterEach(() => {
    client.clear();
  });

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('starts without a package and asks to choose one when the form is sent', () => {
    const { result } = renderHook(() => useNewBookingForm(), { wrapper: Wrapper });

    expect(result.current.values.packageName).toBe('');
    act(() => result.current.submit());

    expect(result.current.errors.packageName).toBe('Elige un paquete.');
    expect(bookingsApi.createBooking).not.toHaveBeenCalled();
  });

  it('stores the chosen package and clears its validation message', () => {
    const { result } = renderHook(() => useNewBookingForm(), { wrapper: Wrapper });
    act(() => result.current.submit());

    act(() => result.current.setPackage('Retrato Familiar'));

    expect(result.current.values.packageName).toBe('Retrato Familiar');
    expect(result.current.errors.packageName).toBeUndefined();
  });

  it('sends the chosen package as the package name and opens the new booking', async () => {
    const created = parseBooking(bookingPayload);
    jest.mocked(bookingsApi.createBooking).mockResolvedValue(created);
    const { result } = renderHook(() => useNewBookingForm(), { wrapper: Wrapper });

    act(() => {
      result.current.setText('clientName', 'María Pérez');
      result.current.setPhone('8888 1111');
      result.current.setPackage('Pre-Quinceaños');
      result.current.setText('price', '80000');
    });
    act(() => result.current.submit());

    await waitFor(() => expect(replaceWithBooking).toHaveBeenCalledWith(created.id));
    expect(bookingsApi.createBooking).toHaveBeenCalledWith(
      expect.objectContaining({ packageName: 'Pre-Quinceaños', clientName: 'María Pérez', packagePrice: 80000 }),
    );
  });

  it('shows the phone as 0000-0000 while it is typed but sends it in international format', async () => {
    jest.mocked(bookingsApi.createBooking).mockResolvedValue(parseBooking(bookingPayload));
    const { result } = renderHook(() => useNewBookingForm(), { wrapper: Wrapper });

    act(() => result.current.setPhone('70189220'));

    expect(result.current.values.clientPhone).toBe('7018-9220');

    act(() => {
      result.current.setText('clientName', 'María Pérez');
      result.current.setPackage('Producto');
      result.current.setText('price', '80000');
    });
    act(() => result.current.submit());

    await waitFor(() => expect(bookingsApi.createBooking).toHaveBeenCalled());
    expect(bookingsApi.createBooking).toHaveBeenCalledWith(expect.objectContaining({ clientPhone: '+50670189220' }));
  });

  it('clears the phone message as soon as the photographer types', () => {
    const { result } = renderHook(() => useNewBookingForm(), { wrapper: Wrapper });
    act(() => result.current.submit());
    expect(result.current.errors.clientPhone).toBeDefined();

    act(() => result.current.setPhone('7'));

    expect(result.current.errors.clientPhone).toBeUndefined();
  });
});
