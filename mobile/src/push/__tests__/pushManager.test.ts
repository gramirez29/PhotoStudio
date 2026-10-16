import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';
import { pushDevicesApi } from '../../api/client';
import { PUSH_UNREGISTER_TIMEOUT_MS } from '../../constants/push';
import {
  configureForegroundNotifications,
  registerPushDevice,
  subscribeToPushEvents,
  unregisterPushDevice,
} from '../pushManager';

jest.mock('../../api/client', () => ({ pushDevicesApi: { register: jest.fn(), unregister: jest.fn() } }));
jest.mock('expo-constants', () => ({ __esModule: true, default: { expoConfig: { extra: { eas: { projectId: 'project-1' } } } } }));
jest.mock('expo-device', () => ({ isDevice: true }));
jest.mock('expo-notifications', () => ({
  AndroidImportance: { HIGH: 4 },
  setNotificationHandler: jest.fn(),
  setNotificationChannelAsync: jest.fn(() => Promise.resolve(null)),
  getPermissionsAsync: jest.fn(),
  requestPermissionsAsync: jest.fn(),
  getExpoPushTokenAsync: jest.fn(),
  addNotificationReceivedListener: jest.fn(),
  addNotificationResponseReceivedListener: jest.fn(),
  getLastNotificationResponse: jest.fn(() => null),
  clearLastNotificationResponse: jest.fn(),
}));

const TOKEN = 'ExponentPushToken[abc]';

/** The configuration of a build that belongs to an Expo project. */
const CONFIG_WITH_PROJECT = { name: 'PhotoSense Studio', slug: 'photostudio', extra: { eas: { projectId: 'project-1' } } };

/** The configuration of a build that has no Expo project. */
const CONFIG_WITHOUT_PROJECT = { name: 'PhotoSense Studio', slug: 'photostudio', extra: {} };

/**
 * Makes the permission status the one the system reports.
 * @param granted Whether notifications are allowed.
 * @param canAskAgain Whether the system still lets the app ask.
 */
function permissions(granted: boolean, canAskAgain = true): void {
  jest.mocked(Notifications.getPermissionsAsync).mockResolvedValue({ granted, canAskAgain } as Notifications.NotificationPermissionsStatus);
}

/**
 * Builds the notification response of a tap with the given data.
 * @param data Data of the notification content.
 * @returns The response.
 */
function tap(data: unknown): Notifications.NotificationResponse {
  return { notification: { request: { content: { data } } } } as Notifications.NotificationResponse;
}

describe('pushManager', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    Platform.OS = 'android';
    jest.mocked(pushDevicesApi.register).mockResolvedValue(undefined);
    jest.mocked(pushDevicesApi.unregister).mockResolvedValue(undefined);
    jest.mocked(Notifications.getExpoPushTokenAsync).mockResolvedValue({ type: 'expo', data: TOKEN });
    jest.mocked(Notifications.getLastNotificationResponse).mockReturnValue(null);
    jest.mocked(Notifications.addNotificationReceivedListener).mockReturnValue({ remove: jest.fn() });
    jest.mocked(Notifications.addNotificationResponseReceivedListener).mockReturnValue({ remove: jest.fn() });
    permissions(true);
  });

  describe('configureForegroundNotifications', () => {
    it('shows the notifications that arrive while the app is open', async () => {
      configureForegroundNotifications();

      const handler = jest.mocked(Notifications.setNotificationHandler).mock.calls[0]?.[0];
      await expect(handler?.handleNotification({} as Notifications.Notification)).resolves.toEqual({
        shouldShowBanner: true,
        shouldShowList: true,
        shouldPlaySound: true,
        shouldSetBadge: false,
      });
    });
  });

  describe('registerPushDevice', () => {
    it('creates the channel, gets the token of the project and registers the phone', async () => {
      await expect(registerPushDevice()).resolves.toBe('registered');

      expect(Notifications.setNotificationChannelAsync).toHaveBeenCalledWith('default', expect.objectContaining({ name: 'Avisos', importance: 4 }));
      expect(Notifications.getExpoPushTokenAsync).toHaveBeenCalledWith({ projectId: 'project-1' });
      expect(pushDevicesApi.register).toHaveBeenCalledWith({ token: TOKEN, platform: 'Android' });
      expect(Notifications.requestPermissionsAsync).not.toHaveBeenCalled();
    });

    it('asks for permission when the system still lets it, and registers when it is granted', async () => {
      permissions(false, true);
      jest.mocked(Notifications.requestPermissionsAsync).mockResolvedValue({ granted: true } as Notifications.NotificationPermissionsStatus);

      await expect(registerPushDevice()).resolves.toBe('registered');
      expect(Notifications.requestPermissionsAsync).toHaveBeenCalledTimes(1);
    });

    it('registers nothing when the photographer denies the permission', async () => {
      permissions(false, true);
      jest.mocked(Notifications.requestPermissionsAsync).mockResolvedValue({ granted: false } as Notifications.NotificationPermissionsStatus);

      await expect(registerPushDevice()).resolves.toBe('denied');
      expect(pushDevicesApi.register).not.toHaveBeenCalled();
    });

    it('does not ask again when the system will not show the question', async () => {
      permissions(false, false);

      await expect(registerPushDevice()).resolves.toBe('denied');
      expect(Notifications.requestPermissionsAsync).not.toHaveBeenCalled();
    });

    it('does not create the Android channel on iOS and registers the phone as iOS', async () => {
      Platform.OS = 'ios';

      await expect(registerPushDevice()).resolves.toBe('registered');
      expect(Notifications.setNotificationChannelAsync).not.toHaveBeenCalled();
      expect(pushDevicesApi.register).toHaveBeenCalledWith({ token: TOKEN, platform: 'Ios' });
    });

    it('does nothing on a system that cannot receive push or on an emulator', async () => {
      Platform.OS = 'web';
      await expect(registerPushDevice()).resolves.toBe('unavailable');

      Platform.OS = 'android';
      jest.replaceProperty(Device, 'isDevice', false);
      await expect(registerPushDevice()).resolves.toBe('unavailable');
      jest.replaceProperty(Device, 'isDevice', true);

      expect(pushDevicesApi.register).not.toHaveBeenCalled();
    });

    it('is unavailable when the build has no Expo project', async () => {
      jest.replaceProperty(Constants, 'expoConfig', CONFIG_WITHOUT_PROJECT);

      await expect(registerPushDevice()).resolves.toBe('unavailable');
      expect(Notifications.getExpoPushTokenAsync).not.toHaveBeenCalled();
      jest.replaceProperty(Constants, 'expoConfig', CONFIG_WITH_PROJECT);
    });

    it('never throws: a failure answers "failed" so the next start tries again', async () => {
      jest.mocked(pushDevicesApi.register).mockRejectedValue(new Error('offline'));
      await expect(registerPushDevice()).resolves.toBe('failed');

      jest.mocked(Notifications.getExpoPushTokenAsync).mockRejectedValue(new Error('no firebase'));
      await expect(registerPushDevice()).resolves.toBe('failed');
    });
  });

  describe('unregisterPushDevice', () => {
    it('forgets the token that was registered', async () => {
      await registerPushDevice();

      await unregisterPushDevice();

      expect(pushDevicesApi.unregister).toHaveBeenCalledWith({ token: TOKEN });
    });

    it('asks Expo for the token again after the app restarted, when notifications are allowed', async () => {
      jest.resetModules();
      const fresh = jest.requireActual<typeof import('../pushManager')>('../pushManager');
      const client = jest.requireMock<typeof import('../../api/client')>('../../api/client');
      jest.mocked(client.pushDevicesApi.unregister).mockResolvedValue(undefined);
      const notifications = jest.requireMock<typeof import('expo-notifications')>('expo-notifications');
      jest.mocked(notifications.getPermissionsAsync).mockResolvedValue({ granted: true } as Notifications.NotificationPermissionsStatus);
      jest.mocked(notifications.getExpoPushTokenAsync).mockResolvedValue({ type: 'expo', data: 'ExponentPushToken[after-restart]' });

      await fresh.unregisterPushDevice();

      expect(client.pushDevicesApi.unregister).toHaveBeenCalledWith({ token: 'ExponentPushToken[after-restart]' });
    });

    it('has nothing to forget when notifications were never allowed', async () => {
      jest.resetModules();
      const fresh = jest.requireActual<typeof import('../pushManager')>('../pushManager');
      const client = jest.requireMock<typeof import('../../api/client')>('../../api/client');
      const notifications = jest.requireMock<typeof import('expo-notifications')>('expo-notifications');
      jest.mocked(notifications.getPermissionsAsync).mockResolvedValue({ granted: false } as Notifications.NotificationPermissionsStatus);

      await fresh.unregisterPushDevice();

      expect(client.pushDevicesApi.unregister).not.toHaveBeenCalled();
    });

    it('ignores a failure of the backend', async () => {
      await registerPushDevice();
      jest.mocked(pushDevicesApi.unregister).mockRejectedValue(new Error('offline'));

      await expect(unregisterPushDevice()).resolves.toBeUndefined();
    });

    it('stops waiting for a backend that does not answer', async () => {
      jest.useFakeTimers();
      try {
        await registerPushDevice();
        jest.mocked(pushDevicesApi.unregister).mockReturnValue(new Promise<void>(() => undefined));

        const finished = jest.fn();
        void unregisterPushDevice().then(finished);
        await jest.advanceTimersByTimeAsync(PUSH_UNREGISTER_TIMEOUT_MS - 1);
        expect(finished).not.toHaveBeenCalled();

        await jest.advanceTimersByTimeAsync(1);
        expect(finished).toHaveBeenCalledTimes(1);
      } finally {
        jest.useRealTimers();
      }
    });
  });

  describe('subscribeToPushEvents', () => {
    it('reports a notification that arrives while the app is open', () => {
      const handlers = { onReceived: jest.fn(), onOpenBooking: jest.fn() };
      subscribeToPushEvents(handlers);

      jest.mocked(Notifications.addNotificationReceivedListener).mock.calls[0]?.[0]({} as Notifications.Notification);

      expect(handlers.onReceived).toHaveBeenCalledTimes(1);
    });

    it('opens the booking of a tapped notification, and ignores one that is not about a booking', () => {
      const handlers = { onReceived: jest.fn(), onOpenBooking: jest.fn() };
      subscribeToPushEvents(handlers);
      const listener = jest.mocked(Notifications.addNotificationResponseReceivedListener).mock.calls[0]?.[0];

      listener?.(tap({ bookingId: 'b1', type: 'PaymentProofSubmitted' }));
      listener?.(tap({ other: true }));

      expect(handlers.onOpenBooking).toHaveBeenCalledTimes(1);
      expect(handlers.onOpenBooking).toHaveBeenCalledWith('b1');
    });

    it('opens the booking of the tap that launched the app, once', () => {
      jest.mocked(Notifications.getLastNotificationResponse).mockReturnValue(tap({ bookingId: 'b9' }));
      const handlers = { onReceived: jest.fn(), onOpenBooking: jest.fn() };

      subscribeToPushEvents(handlers);

      expect(handlers.onOpenBooking).toHaveBeenCalledWith('b9');
      expect(Notifications.clearLastNotificationResponse).toHaveBeenCalledTimes(1);
    });

    it('stops listening when unsubscribed', () => {
      const received = { remove: jest.fn() };
      const tapped = { remove: jest.fn() };
      jest.mocked(Notifications.addNotificationReceivedListener).mockReturnValue(received);
      jest.mocked(Notifications.addNotificationResponseReceivedListener).mockReturnValue(tapped);

      subscribeToPushEvents({ onReceived: jest.fn(), onOpenBooking: jest.fn() })();

      expect(received.remove).toHaveBeenCalledTimes(1);
      expect(tapped.remove).toHaveBeenCalledTimes(1);
    });
  });
});
