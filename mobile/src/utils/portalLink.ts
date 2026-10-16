import type { ShareLinkOutcome } from '../types/utils/portalLink.types';
import { firstName } from './notifications';

/**
 * Writes the WhatsApp message that carries the booking link to the client.
 * @param clientName Full name of the client.
 * @param packageName Name of the booked package.
 * @param url Link the client opens.
 * @param canReceiveCode Whether the booking has a client email. Without it the link only shows the booking.
 * @returns The message in Spanish, ready to be sent.
 */
export function buildLinkMessage(clientName: string, packageName: string, url: string, canReceiveCode: boolean): string {
  const greeting = `Hola ${firstName(clientName)}, aquí está tu reserva de ${packageName} con PhotoSense Studio:`;
  const action = canReceiveCode
    ? 'Ahí puedes ver el detalle y subir tu comprobante de SINPE. Te enviaremos un código a tu correo para confirmar que eres tú.'
    : 'Ahí puedes ver el detalle de tu reserva.';

  return `${greeting}\n${url}\n\n${action}`;
}

/**
 * Explains why sharing the link did not finish.
 * @param outcome How the attempt ended.
 * @returns The message in Spanish, or null when the link was shared.
 */
export function shareLinkOutcomeMessage(outcome: ShareLinkOutcome): string | null {
  switch (outcome) {
    case 'shared':
      return null;
    case 'invalid-phone':
      return 'El teléfono del cliente no es válido para WhatsApp.';
    case 'whatsapp-unavailable':
      return 'No se pudo abrir WhatsApp. Inténtalo de nuevo.';
  }
}
