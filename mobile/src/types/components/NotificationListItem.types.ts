import type { NotificationResponse } from '../api/notifications';

/** Props of the `NotificationListItem` component. */
export interface NotificationListItemProps {
  /** Notification to display. */
  readonly notification: NotificationResponse;
  /** Called when the photographer taps "Enviar por WhatsApp". */
  readonly onSendWhatsApp: (notification: NotificationResponse) => void;
  /** Called when the photographer taps "Ver reserva". */
  readonly onOpenBooking: (notification: NotificationResponse) => void;
  /** Called when the photographer taps "Marcar como leído"; only offered while the notice is unread. */
  readonly onMarkRead: (notification: NotificationResponse) => void;
}
