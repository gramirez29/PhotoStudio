import { NOTIFICATION_TYPES } from '../constants/notifications';
import type { HttpClient } from '../types/api/http';
import type { MarkAllReadResponse, NotificationListResponse, NotificationResponse } from '../types/api/notifications';
import type { NotificationsApi } from '../types/api/notificationsApi';
import { buildPagedPath } from '../utils/pagination';
import { parseMoney } from './bookingsApi';
import { expectRecord, readArray, readLiteral, readNullableString, readNumber, readString } from './guards';

/**
 * Parses a notification, validating every field against the backend contract.
 * @param value Unknown response body or list element.
 * @param path Location of the value, for error messages.
 * @returns The typed notification.
 */
export function parseNotification(value: unknown, path = 'notification'): NotificationResponse {
  const record = expectRecord(value, path);
  const balance = record.balance;
  const proofAmount = record.proofAmount;
  return {
    id: readString(record, 'id', path),
    type: readLiteral(record, 'type', NOTIFICATION_TYPES, path),
    bookingId: readString(record, 'bookingId', path),
    clientName: readString(record, 'clientName', path),
    clientPhone: readString(record, 'clientPhone', path),
    packageName: readString(record, 'packageName', path),
    sessionStart: readString(record, 'sessionStart', path),
    balance: balance === null || balance === undefined ? null : parseMoney(balance, `${path}.balance`),
    proofAmount: proofAmount === null || proofAmount === undefined ? null : parseMoney(proofAmount, `${path}.proofAmount`),
    selectedPhotos: readOptionalCount(record, 'selectedPhotos', path),
    extraPhotos: readOptionalCount(record, 'extraPhotos', path),
    deliveredAt: readString(record, 'deliveredAt', path),
    readAt: readNullableString(record, 'readAt', path),
  };
}

/**
 * Reads a count that is only present for some notifications.
 * @param record Object to read from.
 * @param key Property name.
 * @param path Location of the object, for error messages.
 * @returns The count, or null when the notification does not carry one.
 */
function readOptionalCount(record: Record<string, unknown>, key: string, path: string): number | null {
  const value = record[key];
  return value === null || value === undefined ? null : readNumber(record, key, path);
}

/**
 * Tells whether a list element is a notification of a type this version of the app knows. The backend may learn to send a new type
 * before the app learns to word it; such a notice is left out of the inbox instead of breaking the whole list.
 * @param value Unknown list element.
 * @returns True when the element has a known type.
 */
function hasKnownType(value: unknown): boolean {
  return typeof value === 'object' && value !== null && NOTIFICATION_TYPES.some((type) => type === (value as { type?: unknown }).type);
}

/**
 * Parses the inbox.
 * @param value Unknown response body.
 * @returns The typed inbox.
 */
export function parseNotificationList(value: unknown): NotificationListResponse {
  const path = 'notifications';
  const record = expectRecord(value, path);
  return {
    items: readArray(record, 'items', path)
      .map((item, index) => ({ item, index }))
      .filter(({ item }) => hasKnownType(item) || typeof item !== 'object' || item === null)
      .map(({ item, index }) => parseNotification(item, `${path}.items[${index}]`)),
    unreadCount: readNumber(record, 'unreadCount', path),
    nextCursor: readNullableString(record, 'nextCursor', path),
  };
}

/**
 * Parses the outcome of marking the whole inbox as read.
 * @param value Unknown response body.
 * @returns The typed outcome.
 */
export function parseMarkAllRead(value: unknown): MarkAllReadResponse {
  const path = 'markAllRead';
  return { marked: readNumber(expectRecord(value, path), 'marked', path) };
}

/**
 * Creates the notifications API on top of an HTTP client.
 * @param client HTTP client.
 * @returns The notifications API.
 */
export function createNotificationsApi(client: HttpClient): NotificationsApi {
  return {
    listNotifications: (cursor, signal) => client.get(buildPagedPath('/api/notifications', cursor), parseNotificationList, signal),
    markRead: (notificationId, signal) =>
      client.post(
        `/api/notifications/${encodeURIComponent(notificationId)}/read`,
        undefined,
        (body) => parseNotification(body),
        signal,
      ),
    markAllRead: (signal) => client.post('/api/notifications/read-all', undefined, parseMarkAllRead, signal),
  };
}
