import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';
import { pushDevicesApi } from '../api/client';
import {
  ANDROID_NOTIFICATION_CHANNEL_ID,
  ANDROID_NOTIFICATION_CHANNEL_NAME,
  PUSH_UNREGISTER_TIMEOUT_MS,
} from '../constants/push';
import type { PushEventHandlers, PushRegistrationOutcome } from '../types/push/pushManager.types';
import { readBookingId, readProjectId, toPushPlatform } from '../utils/push';

/** The token of this phone once it was registered, so signing out does not have to ask for it again. */
let registeredToken: string | null = null;

/**
 * Makes the notifications that arrive while the app is open show as a banner like any other, instead of being swallowed by
 * the app. Call it once when the app starts.
 */
export function configureForegroundNotifications(): void {
  Notifications.setNotificationHandler({
    handleNotification: () =>
      Promise.resolve({ shouldShowBanner: true, shouldShowList: true, shouldPlaySound: true, shouldSetBadge: false }),
  });
}

/**
 * Creates the Android channel the backend sends to. Android refuses to show a notification whose channel does not exist, and
 * the channel must exist before the first token is requested.
 * @returns A promise that resolves when the channel is in place.
 */
async function ensureAndroidChannel(): Promise<void> {
  if (Platform.OS !== 'android') {
    return;
  }

  await Notifications.setNotificationChannelAsync(ANDROID_NOTIFICATION_CHANNEL_ID, {
    name: ANDROID_NOTIFICATION_CHANNEL_NAME,
    importance: Notifications.AndroidImportance.HIGH,
  });
}

/**
 * Checks whether the app may show notifications, asking the photographer when the system still lets it ask.
 * @returns True when notifications are allowed.
 */
async function ensurePermission(): Promise<boolean> {
  const current = await Notifications.getPermissionsAsync();
  if (current.granted) {
    return true;
  }

  if (!current.canAskAgain) {
    return false;
  }

  return (await Notifications.requestPermissionsAsync()).granted;
}

/**
 * Asks Expo for the push token of this phone.
 * @returns The token, or null when this build has no Expo project to ask for it.
 */
async function fetchPushToken(): Promise<string | null> {
  const projectId = readProjectId(Constants.expoConfig?.extra);
  if (projectId === null) {
    return null;
  }

  return (await Notifications.getExpoPushTokenAsync({ projectId })).data;
}

/**
 * Registers this phone with the backend so it can push to it: creates the Android channel, asks for permission, gets the
 * token and sends it. It never throws, because notifications are a convenience and must not get in the way of signing in;
 * whatever fails is retried the next time the app starts or the photographer signs in.
 * @returns What happened.
 */
export async function registerPushDevice(): Promise<PushRegistrationOutcome> {
  const platform = toPushPlatform(Platform.OS);
  if (platform === null || !Device.isDevice) {
    return 'unavailable';
  }

  try {
    await ensureAndroidChannel();
    if (!(await ensurePermission())) {
      return 'denied';
    }

    const token = await fetchPushToken();
    if (token === null) {
      return 'unavailable';
    }

    await pushDevicesApi.register({ token, platform });
    registeredToken = token;
    return 'registered';
  } catch {
    return 'failed';
  }
}

/**
 * Finds the token to forget: the one registered in this run, or, after the app restarted, the one Expo gives again when the
 * photographer allowed notifications.
 * @returns The token, or null when this phone has none.
 */
async function tokenToForget(): Promise<string | null> {
  if (registeredToken !== null) {
    return registeredToken;
  }

  const permissions = await Notifications.getPermissionsAsync();
  return permissions.granted ? fetchPushToken() : null;
}

/**
 * Tells the backend to stop pushing to this phone, so the next person who signs in on it does not get the notices of the
 * previous one. Best effort and bounded in time: signing out must work without a connection, so a failure, or a backend that
 * does not answer within {@link PUSH_UNREGISTER_TIMEOUT_MS}, is ignored.
 * @returns A promise that resolves when the backend forgot the phone, or when waiting for it is no longer worth it.
 */
export async function unregisterPushDevice(): Promise<void> {
  const forget = async (): Promise<void> => {
    const token = await tokenToForget();
    if (token === null) {
      return;
    }

    await pushDevicesApi.unregister({ token });
    registeredToken = null;
  };

  let timer: ReturnType<typeof setTimeout> | undefined;
  const timeout = new Promise<void>((resolve) => {
    timer = setTimeout(resolve, PUSH_UNREGISTER_TIMEOUT_MS);
  });

  try {
    await Promise.race([forget().catch(() => undefined), timeout]);
  } finally {
    clearTimeout(timer);
  }
}

/**
 * Listens to the push events of the app: a notification that arrives while it is open, and a tap on a notification, either
 * while the app runs or the one that opened it from closed.
 * @param handlers What to do on each event.
 * @returns A function that stops listening.
 */
export function subscribeToPushEvents(handlers: PushEventHandlers): () => void {
  const received = Notifications.addNotificationReceivedListener(() => handlers.onReceived());

  /**
   * Opens the booking a tapped notification is about.
   * @param response What the photographer tapped.
   */
  const open = (response: Notifications.NotificationResponse): void => {
    const bookingId = readBookingId(response.notification.request.content.data);
    if (bookingId !== null) {
      handlers.onOpenBooking(bookingId);
    }
  };

  const tapped = Notifications.addNotificationResponseReceivedListener(open);

  // The tap that launched the app from closed happened before the listener existed.
  const launchedBy = Notifications.getLastNotificationResponse();
  if (launchedBy !== null) {
    open(launchedBy);
    Notifications.clearLastNotificationResponse();
  }

  return () => {
    received.remove();
    tapped.remove();
  };
}
