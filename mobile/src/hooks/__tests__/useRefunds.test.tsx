import { act, renderHook, waitFor } from '@testing-library/react-native';
import { billingApi } from '../../api/client';
import { ApiError } from '../../api/httpClient';
import { pendingSettlementPayload } from '../../api/__fixtures__/billingPayloads';
import { parseSettlement } from '../../api/billingApi';
import { goBack, openBooking, openRefund } from '../../navigation/appNavigation';
import { openExternalUrl } from '../../navigation/externalLinks';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useRefundActions } from '../useRefundActions';
import { useRefundForm } from '../useRefundForm';
import { useSettlement } from '../useSettlement';

jest.mock('../../api/client', () => ({
  billingApi: { listRefunds: jest.fn(), getSettlement: jest.fn(), completeRefund: jest.fn() },
}));
jest.mock('../../navigation/appNavigation');
jest.mock('../../navigation/externalLinks', () => ({ openExternalUrl: jest.fn() }));

const settlement = parseSettlement(pendingSettlementPayload);

describe('useSettlement', () => {
  const { client, Wrapper } = createQueryWrapper();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  afterEach(() => {
    client.clear();
  });

  it('returns the settlement of the booking', async () => {
    jest.mocked(billingApi.getSettlement).mockResolvedValue(settlement);
    const { result } = renderHook(() => useSettlement(settlement.bookingId, true), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.data).toEqual(settlement));
  });

  it('treats a 404 as "nothing to settle" instead of a failure', async () => {
    jest.mocked(billingApi.getSettlement).mockRejectedValue(new ApiError(404, 'Not found', 'resource.not_found'));
    const { result } = renderHook(() => useSettlement(settlement.bookingId, true), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toBeNull();
  });

  it('keeps other failures as errors', async () => {
    jest.mocked(billingApi.getSettlement).mockRejectedValue(new ApiError(500, 'Server error', 'server.error'));
    const { result } = renderHook(() => useSettlement(settlement.bookingId, true), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });

  it('does not ask while disabled', () => {
    renderHook(() => useSettlement(settlement.bookingId, false), { wrapper: Wrapper });

    expect(billingApi.getSettlement).not.toHaveBeenCalled();
  });
});

describe('useRefundForm', () => {
  const { client, Wrapper } = createQueryWrapper();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  afterEach(() => {
    client.clear();
  });

  it('starts with SINPE Móvil, the usual way to give money back', () => {
    const { result } = renderHook(() => useRefundForm(settlement.bookingId), { wrapper: Wrapper });

    expect(result.current.method).toBe('SinpeMovil');
  });

  it('does not send a note that is too long', () => {
    const { result } = renderHook(() => useRefundForm(settlement.bookingId), { wrapper: Wrapper });
    act(() => result.current.setNote('a'.repeat(300)));

    act(() => result.current.submit());

    expect(result.current.validationError).toContain('La nota no puede pasar de');
    expect(billingApi.completeRefund).not.toHaveBeenCalled();
  });

  it('records the refund and goes back when the server accepts it', async () => {
    jest.mocked(billingApi.completeRefund).mockResolvedValue({ ...settlement, refundStatus: 'Completed' });
    const { result } = renderHook(() => useRefundForm(settlement.bookingId), { wrapper: Wrapper });
    act(() => result.current.setMethod('Cash'));
    act(() => result.current.setNote(' pagado '));

    act(() => result.current.submit());

    await waitFor(() => expect(goBack).toHaveBeenCalledTimes(1));
    expect(billingApi.completeRefund).toHaveBeenCalledWith(settlement.bookingId, { method: 'Cash', note: 'pagado' });
  });

  it('shows why when the refund was already completed', async () => {
    jest.mocked(billingApi.completeRefund).mockRejectedValue(new ApiError(409, 'Conflict', 'booking.invalid_transition'));
    const { result } = renderHook(() => useRefundForm(settlement.bookingId), { wrapper: Wrapper });

    act(() => result.current.submit());

    await waitFor(() => expect(result.current.submitError).toContain('ya no está pendiente'));
    expect(goBack).not.toHaveBeenCalled();
  });
});

describe('useRefundActions', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('opens WhatsApp with the message for the client', async () => {
    jest.mocked(openExternalUrl).mockResolvedValue(true);
    const { result } = renderHook(() => useRefundActions());

    act(() => result.current.contact(settlement));

    await waitFor(() => expect(openExternalUrl).toHaveBeenCalledTimes(1));
    expect(jest.mocked(openExternalUrl).mock.calls[0]?.[0]).toMatch(/^https:\/\/wa\.me\/50670189220\?text=Hola%20Mar/);
    expect(result.current.error).toBeNull();
  });

  it('explains when WhatsApp cannot be opened or the client has no phone', async () => {
    jest.mocked(openExternalUrl).mockResolvedValue(false);
    const { result } = renderHook(() => useRefundActions());

    act(() => result.current.contact(settlement));
    await waitFor(() => expect(result.current.error).toBe('No se pudo abrir WhatsApp en este dispositivo.'));

    act(() => result.current.contact({ ...settlement, clientPhone: '' }));
    await waitFor(() => expect(result.current.error).toBe('Este cliente no tiene un teléfono válido para abrir WhatsApp.'));
    expect(openExternalUrl).toHaveBeenCalledTimes(1);
  });

  it('opens the refund form and the booking', () => {
    const { result } = renderHook(() => useRefundActions());

    act(() => result.current.complete(settlement));
    act(() => result.current.openBooking(settlement));

    expect(openRefund).toHaveBeenCalledWith(settlement.bookingId);
    expect(openBooking).toHaveBeenCalledWith(settlement.bookingId);
  });
});
