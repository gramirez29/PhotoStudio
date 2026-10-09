import type { MarkAllReadResponse, NotificationListResponse, NotificationResponse } from './notifications';

/** Notification endpoints used by the photographer app. */
export interface NotificationsApi {
  /**
   * Loads the inbox.
   * @param signal Optional signal to cancel the request.
   * @returns The notifications and the unread count.
   */
  listNotifications(signal?: AbortSignal): Promise<NotificationListResponse>;

  /**
   * Marks one notification as read.
   * @param notificationId Notification identifier.
   * @param signal Optional signal to cancel the request.
   * @returns The notification after being marked.
   */
  markRead(notificationId: string, signal?: AbortSignal): Promise<NotificationResponse>;

  /**
   * Marks every unread notification as read.
   * @param signal Optional signal to cancel the request.
   * @returns How many were unread.
   */
  markAllRead(signal?: AbortSignal): Promise<MarkAllReadResponse>;
}
