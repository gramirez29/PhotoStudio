import { useMutation } from '@tanstack/react-query';
import { Alert } from 'react-native';
import { bookingsApi } from '../api/client';
import { openExternalUrl } from '../navigation/externalLinks';
import type { BookingResponse } from '../types/api/booking';
import type { ShareLinkState } from '../types/hooks/useShareLink.types';
import type { ShareLinkOutcome } from '../types/utils/portalLink.types';
import { NETWORK_ERROR_MESSAGE } from '../utils/apiErrors';
import { buildWhatsAppUrl } from '../utils/notifications';
import { buildLinkMessage, shareLinkOutcomeMessage } from '../utils/portalLink';

/**
 * Creates the link of a booking and opens WhatsApp with it ready to send. Creating a link cancels the previous one, so the
 * photographer is asked first. The token of a link exists only in the answer that creates it: if WhatsApp cannot be opened
 * afterwards, the link is lost and sharing again creates another.
 * @param booking Booking whose link is shared.
 * @returns The state of the attempt and the action that starts it.
 */
export function useShareLink(booking: BookingResponse): ShareLinkState {
  // Not retried: every attempt creates a new link and cancels the previous one.
  const sharing = useMutation<ShareLinkOutcome, Error>({
    mutationFn: async () => {
      const link = await bookingsApi.createBookingLink(booking.id);
      const message = buildLinkMessage(booking.clientName, booking.packageName, link.url, booking.clientEmail !== null);
      const whatsAppUrl = buildWhatsAppUrl(booking.clientPhone, message);
      if (whatsAppUrl === null) {
        return 'invalid-phone';
      }

      return (await openExternalUrl(whatsAppUrl)) ? 'shared' : 'whatsapp-unavailable';
    },
  });

  let errorMessage: string | null = null;
  if (sharing.isError) {
    errorMessage = NETWORK_ERROR_MESSAGE;
  } else if (sharing.data !== undefined) {
    errorMessage = shareLinkOutcomeMessage(sharing.data);
  }

  return {
    isPending: sharing.isPending,
    errorMessage,
    share: () =>
      Alert.alert(
        'Enlace para el cliente',
        'Se crea un enlace nuevo y se abre WhatsApp para enviarlo. Si ya habías enviado un enlace de esta reserva, dejará de funcionar.',
        [
          { text: 'Volver', style: 'cancel' },
          { text: 'Crear y enviar', onPress: () => sharing.mutate() },
        ],
      ),
  };
}
