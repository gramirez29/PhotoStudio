import { act, renderHook, waitFor } from '@testing-library/react-native';
import { notificationsApi } from '../../api/client';
import { openBooking } from '../../navigation/appNavigation';
import { openExternalUrl } from '../../navigation/externalLinks';
import type { NotificationResponse } from '../../types/api/notifications';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useNotificationActions } from '../useNotificationActions';

jest.mock('../../api/client', () => ({
  notificationsApi: { markRead: jest.fn(), markAllRead: jest.fn(), listNotifications: jest.fn() },
}));
jest.mock('../../navigation/appNavigation', () => ({ openBooking: jest.fn() }));
jest.mock('../../navigation/externalLinks', () => ({ openExternalUrl: jest.fn() }));

/**
 * Builds a notification, unread unless overridden.
 * @param overrides Values to replace.
 * @returns The notification.
 */
function notification(overrides: Partial<NotificationResponse> = {}): NotificationResponse {
  return {
    id: 'n1',
    type: 'SessionReminder',
    bookingId: 'b1',
    clientName: 'María Pérez',
    clientPhone: '+50670189220',
    packageName: 'Retrato Familiar',
    sessionStart: '2026-10-17T21:00:00+00:00',
    balance: null,
    deliveredAt: '2026-10-16T21:00:00+00:00',
    readAt: null,
    ...overrides,
  };
}

describe('useNotificationActions', () => {
  const { client, Wrapper } = createQueryWrapper();

  beforeEach(() => {
    jest.clearAllMocks();
    jest.mocked(notificationsApi.markRead).mockResolvedValue(notification({ readAt: '2026-10-17T00:00:00+00:00' }));
  });

  afterEach(() => {
    client.clear();
  });

  it('opens WhatsApp with the client message and marks the notice as read', async () => {
    jest.mocked(openExternalUrl).mockResolvedValue(true);
    const { result } = renderHook(() => useNotificationActions(), { wrapper: Wrapper });

    act(() => result.current.sendWhatsApp(notification()));

    await waitFor(() => expect(notificationsApi.markRead).toHaveBeenCalledWith('n1'));
    expect(jest.mocked(openExternalUrl).mock.calls[0]?.[0]).toMatch(/^https:\/\/wa\.me\/50670189220\?text=Hola%20Mar/);
    expect(result.current.error).toBeNull();
  });

  it('does not mark as read a notice that already was', async () => {
    jest.mocked(openExternalUrl).mockResolvedValue(true);
    const { result } = renderHook(() => useNotificationActions(), { wrapper: Wrapper });

    act(() => result.current.sendWhatsApp(notification({ readAt: '2026-10-17T00:00:00+00:00' })));

    await waitFor(() => expect(openExternalUrl).toHaveBeenCalledTimes(1));
    expect(notificationsApi.markRead).not.toHaveBeenCalled();
  });

  it('keeps the notice unread and explains when the device cannot open WhatsApp', async () => {
    jest.mocked(openExternalUrl).mockResolvedValue(false);
    const { result } = renderHook(() => useNotificationActions(), { wrapper: Wrapper });

    act(() => result.current.sendWhatsApp(notification()));

    await waitFor(() => expect(result.current.error).toBe('No se pudo abrir WhatsApp en este dispositivo.'));
    expect(notificationsApi.markRead).not.toHaveBeenCalled();
  });

  it('explains when the client has no usable phone, without trying to open anything', async () => {
    const { result } = renderHook(() => useNotificationActions(), { wrapper: Wrapper });

    act(() => result.current.sendWhatsApp(notification({ clientPhone: '' })));

    await waitFor(() => expect(result.current.error).toBe('Este cliente no tiene un teléfono válido para abrir WhatsApp.'));
    expect(openExternalUrl).not.toHaveBeenCalled();
  });

  it('opens the booking of the notice', () => {
    const { result } = renderHook(() => useNotificationActions(), { wrapper: Wrapper });

    act(() => result.current.openBooking(notification()));

    expect(openBooking).toHaveBeenCalledWith('b1');
  });

  it('marks one notice and the whole inbox as read', async () => {
    jest.mocked(notificationsApi.markAllRead).mockResolvedValue({ marked: 2 });
    const { result } = renderHook(() => useNotificationActions(), { wrapper: Wrapper });

    act(() => result.current.markRead(notification()));
    act(() => result.current.markAllRead());

    await waitFor(() => expect(notificationsApi.markRead).toHaveBeenCalledWith('n1'));
    await waitFor(() => expect(notificationsApi.markAllRead).toHaveBeenCalledTimes(1));
  });
});
