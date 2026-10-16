import { PUSH_PLATFORMS } from '../constants/push';
import type { PushPlatform } from '../types/api/pushDevices';
import type { JsonRecord } from '../types/api/json';

/**
 * Checks whether a value is a plain object whose properties can be read.
 * @param value Value to check.
 * @returns True for non-null, non-array objects.
 */
function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * Reads the identifier of the Expo project from the `extra` section of the app configuration, where `eas init` writes it.
 * It is needed to ask for a push token.
 * @param extra The `extra` value of the Expo configuration, which can be anything.
 * @returns The project identifier, or null when the configuration has none.
 */
export function readProjectId(extra: unknown): string | null {
  if (!isRecord(extra) || !isRecord(extra.eas)) {
    return null;
  }

  const { projectId } = extra.eas;
  return typeof projectId === 'string' && projectId.length > 0 ? projectId : null;
}

/**
 * Reads the booking a notification is about from the data the backend attaches to every push. The data comes from outside the
 * app, so nothing about it is trusted: an unexpected shape just means the tap opens nothing.
 * @param data The `data` of the notification content, which can be anything.
 * @returns The booking identifier, or null when the notification is not about a booking.
 */
export function readBookingId(data: unknown): string | null {
  if (!isRecord(data)) {
    return null;
  }

  const { bookingId } = data;
  return typeof bookingId === 'string' && bookingId.trim().length > 0 ? bookingId : null;
}

/**
 * Maps the operating system reported by React Native to the platform the backend knows.
 * @param os Value of `Platform.OS`.
 * @returns The backend platform, or null for systems that do not receive push (web, Windows, macOS).
 */
export function toPushPlatform(os: string): PushPlatform | null {
  const wanted = os.toLowerCase();
  return PUSH_PLATFORMS.find((platform) => platform.toLowerCase() === wanted) ?? null;
}
