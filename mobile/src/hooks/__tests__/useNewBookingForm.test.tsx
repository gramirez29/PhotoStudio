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

  it('starts without a type of session or a package and asks to choose them when the form is sent', () => {
    const { result } = renderHook(() => useNewBookingForm(), { wrapper: Wrapper });

    expect(result.current.values.packageName).toBe('');
    expect(result.current.values.packageTier).toBeNull();
    act(() => result.current.submit());

    expect(result.current.errors.packageName).toBe('Elige un tipo de sesión.');
    expect(result.current.errors.packageTier).toBe('Elige un paquete.');
    expect(bookingsApi.createBooking).not.toHaveBeenCalled();
  });

  it('stores the chosen type of session and clears its validation message', () => {
    const { result } = renderHook(() => useNewBookingForm(), { wrapper: Wrapper });
    act(() => result.current.submit());

    act(() => result.current.setSessionType('Retrato Familiar'));

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
      result.current.setSessionType('Pre-Quinceaños');
      result.current.setPackageTier('Estándar');
      result.current.setText('price', '80000');
    });
    act(() => result.current.submit());

    await waitFor(() => expect(replaceWithBooking).toHaveBeenCalledWith(created.id));
    expect(bookingsApi.createBooking).toHaveBeenCalledWith(
      expect.objectContaining({ packageName: 'Pre-Quinceaños', packageTier: 'Standard', clientName: 'María Pérez', packagePrice: 80000 }),
    );
  });

  it('shows the phone as 0000-0000 while it is typed but sends it in international format', async () => {
    jest.mocked(bookingsApi.createBooking).mockResolvedValue(parseBooking(bookingPayload));
    const { result } = renderHook(() => useNewBookingForm(), { wrapper: Wrapper });

    act(() => result.current.setPhone('70189220'));

    expect(result.current.values.clientPhone).toBe('7018-9220');

    act(() => {
      result.current.setText('clientName', 'María Pérez');
      result.current.setSessionType('Producto');
      result.current.setPackageTier('Básico');
      result.current.setText('price', '80000');
    });
    act(() => result.current.submit());

    await waitFor(() => expect(bookingsApi.createBooking).toHaveBeenCalled());
    expect(bookingsApi.createBooking).toHaveBeenCalledWith(expect.objectContaining({ clientPhone: '+50670189220' }));
  });

  it('fills in the price and the length of the session when a package that has them is chosen', () => {
    const { result } = renderHook(() => useNewBookingForm(), { wrapper: Wrapper });

    act(() => result.current.setPackageTier('Premium'));

    expect(result.current.values.packageTier).toBe('Premium');
    expect(result.current.values.price).toBe('110000');
    expect(result.current.values.durationHours).toBe(3);
    expect(result.current.totals.total).toBe(110000);
  });

  it('leaves the length and what was typed alone for a package without a price, but drops the price the last package filled in', () => {
    const { result } = renderHook(() => useNewBookingForm(), { wrapper: Wrapper });
    act(() => result.current.setPackageTier('Básico'));
    act(() => result.current.setDuration(4));

    act(() => result.current.setPackageTier('Custom'));

    expect(result.current.values.price).toBe('');
    expect(result.current.values.durationHours).toBe(4);
    act(() => result.current.setText('price', '90000'));
    act(() => result.current.setPackageTier('Otro'));
    expect(result.current.values.price).toBe('90000');
  });

  it('ignores a package name that is not in the configuration', () => {
    const { result } = renderHook(() => useNewBookingForm(), { wrapper: Wrapper });

    act(() => result.current.setPackageTier('No existe'));

    expect(result.current.values.packageTier).toBeNull();
  });

  it('adds the extras and subtracts the discount in the total to pay', () => {
    const { result } = renderHook(() => useNewBookingForm(), { wrapper: Wrapper });
    act(() => result.current.setPackageTier('Estándar'));

    act(() => result.current.setExtras([{ code: 'makeup', name: 'Maquillaje', unitPrice: 20000, quantity: 2 }]));
    act(() => result.current.setText('discount', '5000'));

    expect(result.current.totals).toEqual({ packagePrice: 65000, extrasTotal: 40000, discount: 5000, total: 100000 });
  });

  it('sends the package, the extras and the discount when the booking is created', async () => {
    jest.mocked(bookingsApi.createBooking).mockResolvedValue(parseBooking(bookingPayload));
    const { result } = renderHook(() => useNewBookingForm(), { wrapper: Wrapper });
    act(() => {
      result.current.setText('clientName', 'María Pérez');
      result.current.setPhone('70189220');
      result.current.setSessionType('Producto');
      result.current.setPackageTier('Básico');
      result.current.setExtras([{ code: 'makeup', name: 'Maquillaje', unitPrice: 20000, quantity: 1 }]);
      result.current.setText('discount', '5000');
    });

    act(() => result.current.submit());

    await waitFor(() => expect(bookingsApi.createBooking).toHaveBeenCalled());
    expect(bookingsApi.createBooking).toHaveBeenCalledWith(
      expect.objectContaining({
        packageTier: 'Basic',
        packagePrice: 35000,
        extras: [{ code: 'makeup', name: 'Maquillaje', unitPrice: 20000, quantity: 1 }],
        discount: 5000,
      }),
    );
  });

  it('clears the phone message as soon as the photographer types', () => {
    const { result } = renderHook(() => useNewBookingForm(), { wrapper: Wrapper });
    act(() => result.current.submit());
    expect(result.current.errors.clientPhone).toBeDefined();

    act(() => result.current.setPhone('7'));

    expect(result.current.errors.clientPhone).toBeUndefined();
  });
});
