import { act, renderHook, waitFor } from '@testing-library/react-native';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { bookingsApi } from '../../api/client';
import { replaceWithBooking } from '../../navigation/appNavigation';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useNewBookingForm } from '../useNewBookingForm';

jest.mock('../../config/env', () => ({
  env: { apiUrl: 'https://api.example.test', photographerId: '0197a000-0000-7000-8000-000000000001' },
}));
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
      result.current.setText('clientPhone', '8888 1111');
      result.current.setPackage('Pre-Quinceaños');
      result.current.setText('price', '80000');
    });
    act(() => result.current.submit());

    await waitFor(() => expect(replaceWithBooking).toHaveBeenCalledWith(created.id));
    expect(bookingsApi.createBooking).toHaveBeenCalledWith(
      expect.objectContaining({ packageName: 'Pre-Quinceaños', clientName: 'María Pérez', packagePrice: 80000 }),
    );
  });
});
