import type { NotificationResponse } from '../api/notifications';

/** What the inbox screen can do with its notices. */
export interface NotificationActions {
  /** Message of the last failure to open WhatsApp, or null when there is none. */
  readonly error: string | null;
  /** Opens WhatsApp with the message for the client and marks the notice as read. */
  readonly sendWhatsApp: (notification: NotificationResponse) => void;
  /** Opens the booking the notice is about. */
  readonly openBooking: (notification: NotificationResponse) => void;
  /** Marks one notice as read. */
  readonly markRead: (notification: NotificationResponse) => void;
  /** Marks the whole inbox as read. */
  readonly markAllRead: () => void;
  /** Whether marking the whole inbox is in progress. */
  readonly isMarkingAll: boolean;
}
