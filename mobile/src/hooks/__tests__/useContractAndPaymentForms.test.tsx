import { act, renderHook, waitFor } from '@testing-library/react-native';
import { bookingsApi } from '../../api/client';
import { parseBooking } from '../../api/bookingsApi';
import { ApiError } from '../../api/httpClient';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { goBack } from '../../navigation/appNavigation';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useContractForm } from '../useContractForm';
import { usePaymentForm } from '../usePaymentForm';

jest.mock('../../api/client', () => ({
  bookingsApi: {
    getContract: jest.fn(),
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
    jest.mocked(bookingsApi.getContract).mockResolvedValue({ templateVersion: 'v7', text: 'CONTRATO\nPara María Pérez', signed: false });
  });

  afterEach(() => {
    client.clear();
  });

  /**
   * Types everything the client gives on the phone.
   * @param result Hook result holder.
   * @param result.current Latest state of the hook.
   */
  function fillDeclaration(result: { readonly current: ReturnType<typeof useContractForm> }): void {
    act(() => {
      result.current.setText('idNumber', '1-1234-5678');
      result.current.setText('participantCount', '3');
      result.current.setText('sessionLocation', 'Parque La Sabana');
      result.current.setChoice('imageUse', 'Authorized');
      result.current.setChoice('accepted', true);
    });
  }

  it('starts with the client as signer, signing on the phone, and loads the contract to read', async () => {
    const { result } = renderHook(() => useContractForm(booking), { wrapper: Wrapper });

    expect(result.current.values.signerName).toBe(booking.clientName);
    expect(result.current.values.kind).toBe('InPerson');
    expect(result.current.showsContract).toBe(true);
    expect(result.current.isContractLoading).toBe(true);
    await waitFor(() => expect(result.current.contractText).toBe('CONTRATO\nPara María Pérez'));
    expect(bookingsApi.getContract).toHaveBeenCalledWith(booking.id, expect.anything());
  });

  it('does not send anything until the contract has been read', () => {
    jest.mocked(bookingsApi.getContract).mockReturnValue(new Promise(() => undefined));
    const { result } = renderHook(() => useContractForm(booking), { wrapper: Wrapper });
    fillDeclaration(result);

    act(() => result.current.submit());

    expect(bookingsApi.signContractInPerson).not.toHaveBeenCalled();
  });

  it('says what is missing, next to each field, and does not send', async () => {
    const { result } = renderHook(() => useContractForm(booking), { wrapper: Wrapper });
    await waitFor(() => expect(result.current.contractText).not.toBeNull());
    act(() => result.current.setText('signerName', '  '));

    act(() => result.current.submit());

    expect(result.current.errors.signerName).toBe('Escribe el nombre de quien firma.');
    expect(result.current.errors.idNumber).toBeDefined();
    expect(result.current.errors.accepted).toBeDefined();
    expect(bookingsApi.signContractInPerson).not.toHaveBeenCalled();
  });

  it('forgets the message of a field when it is edited', async () => {
    const { result } = renderHook(() => useContractForm(booking), { wrapper: Wrapper });
    await waitFor(() => expect(result.current.contractText).not.toBeNull());
    act(() => result.current.submit());
    expect(result.current.errors.idNumber).toBeDefined();

    act(() => result.current.setText('idNumber', '1'));

    expect(result.current.errors.idNumber).toBeUndefined();
    expect(result.current.errors.participantCount).toBeDefined();
  });

  it('records the signature with the declaration and the version that was read, and goes back', async () => {
    jest.mocked(bookingsApi.signContractInPerson).mockResolvedValue(booking);
    const { result } = renderHook(() => useContractForm(booking), { wrapper: Wrapper });
    await waitFor(() => expect(result.current.contractText).not.toBeNull());
    fillDeclaration(result);

    act(() => result.current.submit());

    await waitFor(() => expect(goBack).toHaveBeenCalledTimes(1));
    expect(bookingsApi.signContractInPerson).toHaveBeenCalledWith(booking.id, {
      signerName: booking.clientName,
      templateVersion: 'v7',
      isPaperContract: false,
      declaration: { idNumber: '112345678', participantCount: 3, sessionLocation: 'Parque La Sabana', imageUse: 'Authorized' },
    });
  });

  it('records a contract on paper with only the name, without reading the contract', async () => {
    jest.mocked(bookingsApi.signContractInPerson).mockResolvedValue(booking);
    const { result } = renderHook(() => useContractForm(booking), { wrapper: Wrapper });
    act(() => result.current.setKind('Paper'));

    act(() => result.current.submit());

    await waitFor(() => expect(goBack).toHaveBeenCalledTimes(1));
    expect(result.current.showsContract).toBe(false);
    expect(bookingsApi.signContractInPerson).toHaveBeenCalledWith(booking.id, {
      signerName: booking.clientName,
      templateVersion: 'v1',
      isPaperContract: true,
    });
  });

  it('explains when the server says the contract changed since it was read', async () => {
    jest.mocked(bookingsApi.signContractInPerson).mockRejectedValue(new ApiError(409, 'changed', 'contract.template_changed'));
    const { result } = renderHook(() => useContractForm(booking), { wrapper: Wrapper });
    await waitFor(() => expect(result.current.contractText).not.toBeNull());
    fillDeclaration(result);

    act(() => result.current.submit());

    await waitFor(() => expect(result.current.submitError).toMatch(/El contrato cambió mientras se leía/));
    expect(goBack).not.toHaveBeenCalled();
  });

  it('shows why the contract could not be loaded, and loads it again on request', async () => {
    jest.mocked(bookingsApi.getContract).mockRejectedValueOnce(new ApiError(500, 'boom', null));
    const { result } = renderHook(() => useContractForm(booking), { wrapper: Wrapper });
    await waitFor(() => expect(result.current.contractError).not.toBeNull());
    expect(result.current.contractText).toBeNull();

    act(() => result.current.reloadContract());

    await waitFor(() => expect(result.current.contractText).toBe('CONTRATO\nPara María Pérez'));
    expect(result.current.contractError).toBeNull();
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
