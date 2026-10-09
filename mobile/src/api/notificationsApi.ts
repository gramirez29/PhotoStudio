import { NOTIFICATION_TYPES } from '../constants/notifications';
import type { HttpClient } from '../types/api/http';
import type { MarkAllReadResponse, NotificationListResponse, NotificationResponse } from '../types/api/notifications';
import type { NotificationsApi } from '../types/api/notificationsApi';
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
  return {
    id: readString(record, 'id', path),
    type: readLiteral(record, 'type', NOTIFICATION_TYPES, path),
    bookingId: readString(record, 'bookingId', path),
    clientName: readString(record, 'clientName', path),
    clientPhone: readString(record, 'clientPhone', path),
    packageName: readString(record, 'packageName', path),
    sessionStart: readString(record, 'sessionStart', path),
    balance: balance === null || balance === undefined ? null : parseMoney(balance, `${path}.balance`),
    deliveredAt: readString(record, 'deliveredAt', path),
    readAt: readNullableString(record, 'readAt', path),
  };
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
    items: readArray(record, 'items', path).map((item, index) => parseNotification(item, `${path}.items[${index}]`)),
    unreadCount: readNumber(record, 'unreadCount', path),
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
    listNotifications: (signal) => client.get('/api/notifications', parseNotificationList, signal),
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
