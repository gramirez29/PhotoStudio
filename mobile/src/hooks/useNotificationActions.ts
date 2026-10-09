import { useState } from 'react';
import { openBooking } from '../navigation/appNavigation';
import { openExternalUrl } from '../navigation/externalLinks';
import type { NotificationResponse } from '../types/api/notifications';
import type { NotificationActions } from '../types/hooks/useNotificationActions.types';
import { whatsAppUrlFor } from '../utils/notifications';
import { useMarkAllNotificationsRead, useMarkNotificationRead } from './useMarkNotificationsRead';

/**
 * Actions of the inbox screen. Sending a notice on WhatsApp also marks it as read, because the photographer has acted on
 * it; if the device cannot open WhatsApp, or the client has no phone, the notice stays unread and a message says why.
 * @returns The actions and the message of the last failure, if any.
 */
export function useNotificationActions(): NotificationActions {
  const markRead = useMarkNotificationRead();
  const markAllRead = useMarkAllNotificationsRead();
  const [error, setError] = useState<string | null>(null);

  /**
   * Opens WhatsApp with the message for the client and marks the notice as read.
   * @param notification Notice to act on.
   */
  const sendWhatsApp = async (notification: NotificationResponse): Promise<void> => {
    const url = whatsAppUrlFor(notification);
    if (url === null) {
      setError('Este cliente no tiene un teléfono válido para abrir WhatsApp.');
      return;
    }

    if (!(await openExternalUrl(url))) {
      setError('No se pudo abrir WhatsApp en este dispositivo.');
      return;
    }

    setError(null);
    if (notification.readAt === null) {
      markRead.mutate(notification.id);
    }
  };

  return {
    error,
    sendWhatsApp: (notification) => void sendWhatsApp(notification),
    openBooking: (notification) => openBooking(notification.bookingId),
    markRead: (notification) => markRead.mutate(notification.id),
    markAllRead: () => markAllRead.mutate(),
    isMarkingAll: markAllRead.isPending,
  };
}
