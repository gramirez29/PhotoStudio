import type { NOTIFICATION_TYPES } from '../../constants/notifications';
import type { MoneyResponse } from './booking';

/** What a notification is about. */
export type NotificationType = (typeof NOTIFICATION_TYPES)[number];

/** A delivered notification, as `GET /api/notifications` returns it. It carries data, not text: the app words it. */
export interface NotificationResponse {
  /** Notification identifier. */
  readonly id: string;
  /** What the notification is about. */
  readonly type: NotificationType;
  /** Booking the notification is about. */
  readonly bookingId: string;
  /** Name of the client. */
  readonly clientName: string;
  /** Phone of the client in international format, used to open a WhatsApp conversation. */
  readonly clientPhone: string;
  /** Name of the booked package. */
  readonly packageName: string;
  /** Start of the session, ISO 8601. */
  readonly sessionStart: string;
  /** Amount still owed; present only for balance notices. */
  readonly balance: MoneyResponse | null;
  /** Amount the client declared in the proof of payment; present only for proof notices. */
  readonly proofAmount: MoneyResponse | null;
  /** How many photos the client chose; present only for selection notices. */
  readonly selectedPhotos: number | null;
  /** How many of the chosen photos are extra (beyond what the package includes); present only for selection notices. */
  readonly extraPhotos: number | null;
  /** Instant the notification reached the inbox, ISO 8601. */
  readonly deliveredAt: string;
  /** Instant the photographer read it, ISO 8601, or null while it is unread. */
  readonly readAt: string | null;
}

/** One page of the inbox of the photographer. */
export interface NotificationListResponse {
  /** Delivered notifications of this page, the most recent first. */
  readonly items: readonly NotificationResponse[];
  /** How many notifications are unread (not only the ones listed). */
  readonly unreadCount: number;
  /** What to send back to get the next page, or null when this is the last one. */
  readonly nextCursor: string | null;
}

/** Outcome of marking the whole inbox as read. */
export interface MarkAllReadResponse {
  /** How many notifications were unread. */
  readonly marked: number;
}
