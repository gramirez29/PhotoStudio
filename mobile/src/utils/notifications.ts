import type { NotificationResponse } from '../types/api/notifications';
import type { NotificationText } from '../types/utils/notifications.types';
import { formatDate, formatDateTime, formatMoney, formatTime } from './format';

/** Base of the links that open a WhatsApp conversation. */
const WHATSAPP_BASE_URL = 'https://wa.me';

/**
 * Words a notification for the inbox: a short title and one line with what to know.
 * @param notification Notification to word.
 * @returns The title and the detail, in Spanish.
 */
export function describeNotification(notification: NotificationResponse): NotificationText {
  if (notification.type === 'BalanceDue' && notification.balance !== null) {
    return {
      title: 'Saldo pendiente',
      detail: `${notification.clientName} debe ${formatMoney(notification.balance)} de su sesión de ${notification.packageName}.`,
    };
  }

  return {
    title: 'Sesión próxima',
    detail: `${notification.clientName} · ${notification.packageName} · ${formatDateTime(notification.sessionStart)}`,
  };
}

/**
 * Gets the first word of a name, to greet the client the way one writes on WhatsApp.
 * @param name Full name.
 * @returns The first name, or the whole text when it has no spaces.
 */
export function firstName(name: string): string {
  return name.trim().split(/\s+/)[0] ?? name;
}

/**
 * Writes the WhatsApp message the photographer sends to the client for a notification.
 * @param notification Notification the message is about.
 * @returns The message in Spanish, ready to be sent.
 */
export function buildWhatsAppMessage(notification: NotificationResponse): string {
  const greeting = `Hola ${firstName(notification.clientName)}`;

  if (notification.type === 'BalanceDue' && notification.balance !== null) {
    return (
      `${greeting}, te escribo por el saldo pendiente de ${formatMoney(notification.balance)} ` +
      `de tu sesión de ${notification.packageName}. Quedo atento a tu pago para entregarte las fotos finales. ¡Gracias!`
    );
  }

  const start = new Date(notification.sessionStart);
  return (
    `${greeting}, te recuerdo tu sesión de ${notification.packageName} ` +
    `el ${formatDate(start)} a las ${formatTime(start)}. ¡Nos vemos!`
  );
}

/**
 * Builds the link that opens a WhatsApp conversation with a prefilled message.
 * @param phone Phone as stored, for example `+50670189220`; anything that is not a digit is ignored.
 * @param message Text to prefill.
 * @returns The `wa.me` link, or null when the phone has no digits.
 */
export function buildWhatsAppUrl(phone: string, message: string): string | null {
  const digits = phone.replace(/\D/g, '');
  if (digits.length === 0) {
    return null;
  }

  return `${WHATSAPP_BASE_URL}/${digits}?text=${encodeURIComponent(message)}`;
}

/**
 * Builds the WhatsApp link for a notification.
 * @param notification Notification to act on.
 * @returns The link, or null when the client has no usable phone.
 */
export function whatsAppUrlFor(notification: NotificationResponse): string | null {
  return buildWhatsAppUrl(notification.clientPhone, buildWhatsAppMessage(notification));
}

/**
 * Words the title of the inbox entry on the home screen.
 * @param unreadCount How many notifications are unread.
 * @returns The label, for example "Avisos (2)".
 */
export function inboxLabel(unreadCount: number): string {
  return unreadCount > 0 ? `Avisos (${unreadCount})` : 'Avisos';
}
