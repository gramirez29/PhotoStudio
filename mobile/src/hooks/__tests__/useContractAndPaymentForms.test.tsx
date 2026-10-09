import { act, renderHook, waitFor } from '@testing-library/react-native';
import { bookingsApi } from '../../api/client';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { goBack } from '../../navigation/appNavigation';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useContractForm } from '../useContractForm';
import { usePaymentForm } from '../usePaymentForm';

jest.mock('../../api/client', () => ({
  bookingsApi: {
    signContractInPerson: jest.fn(),
    recordInPersonPayment: jest.fn(),
  },
}));
jest.mock('../../navigation/appNavigation');

const booking = parseBooking(bookingPayload);

describe('useContractForm', () => {
  const { client, Wrapper } = createQueryWrapper();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  afterEach(() => {
    client.clear();
  });

  it('starts with the client as signer, signing on the phone', () => {
    const { result } = renderHook(() => useContractForm(booking), { wrapper: Wrapper });

    expect(result.current.signerName).toBe(booking.clientName);
    expect(result.current.kind).toBe('InPerson');
  });

  it('does not send an empty name', () => {
    const { result } = renderHook(() => useContractForm(booking), { wrapper: Wrapper });
    act(() => result.current.setSignerName('  '));

    act(() => result.current.submit());

    expect(result.current.validationError).toBe('Escribe el nombre de quien firma.');
    expect(bookingsApi.signContractInPerson).not.toHaveBeenCalled();
  });

  it('records the signature and goes back when the server accepts it', async () => {
    jest.mocked(bookingsApi.signContractInPerson).mockResolvedValue(booking);
    const { result } = renderHook(() => useContractForm(booking), { wrapper: Wrapper });
    act(() => result.current.setKind('Paper'));

    act(() => result.current.submit());

    await waitFor(() => expect(goBack).toHaveBeenCalledTimes(1));
    expect(bookingsApi.signContractInPerson).toHaveBeenCalledWith(booking.id, {
      signerName: booking.clientName,
      templateVersion: 'v1',
      isPaperContract: true,
    });
  });
});

describe('usePaymentForm', () => {
  const { client, Wrapper } = createQueryWrapper();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  afterEach(() => {
    client.clear();
  });

  it('suggests an amount and starts with cash', () => {
    const { result } = renderHook(() => usePaymentForm(booking), { wrapper: Wrapper });

    expect(result.current.amount).toMatch(/^\d+$/);
    expect(result.current.method).toBe('Cash');
  });

  it('does not send an invalid amount', () => {
    const { result } = renderHook(() => usePaymentForm(booking), { wrapper: Wrapper });
    act(() => result.current.setAmount('0'));

    act(() => result.current.submit());

    expect(result.current.validationError).toBe('Escribe un monto mayor a cero, en colones.');
    expect(bookingsApi.recordInPersonPayment).not.toHaveBeenCalled();
  });

  it('records the payment and goes back, sending the same key if it is submitted again', async () => {
    jest.mocked(bookingsApi.recordInPersonPayment).mockResolvedValue(booking);
    const { result } = renderHook(() => usePaymentForm(booking), { wrapper: Wrapper });
    act(() => result.current.setAmount('10000'));
    act(() => result.current.setMethod('SinpeMovil'));

    act(() => result.current.submit());
    await waitFor(() => expect(goBack).toHaveBeenCalledTimes(1));
    act(() => result.current.submit());
    await waitFor(() => expect(bookingsApi.recordInPersonPayment).toHaveBeenCalledTimes(2));

    const [first, second] = jest.mocked(bookingsApi.recordInPersonPayment).mock.calls;
    expect(first?.[1]).toMatchObject({ amount: 10000, currency: booking.balance.currency, method: 'SinpeMovil' });
    expect(first?.[1].idempotencyKey).toBe(second?.[1].idempotencyKey);
  });
});
