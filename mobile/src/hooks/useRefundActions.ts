import { useState } from 'react';
import { openBooking, openRefund } from '../navigation/appNavigation';
import { openExternalUrl } from '../navigation/externalLinks';
import type { SettlementResponse } from '../types/api/billing';
import type { RefundActions } from '../types/hooks/useRefundActions.types';
import { refundWhatsAppUrl } from '../utils/billing';

/**
 * Actions of the refunds screen. Writing to the client on WhatsApp does not change the refund: it only helps to find out
 * where to send the money; the refund is completed from its own form once the money is given back.
 * @returns The actions and the message of the last failure, if any.
 */
export function useRefundActions(): RefundActions {
  const [error, setError] = useState<string | null>(null);

  /**
   * Opens WhatsApp with the message for the client.
   * @param settlement Refund to talk about.
   */
  const contact = async (settlement: SettlementResponse): Promise<void> => {
    const url = refundWhatsAppUrl(settlement);
    if (url === null) {
      setError('Este cliente no tiene un teléfono válido para abrir WhatsApp.');
      return;
    }

    setError((await openExternalUrl(url)) ? null : 'No se pudo abrir WhatsApp en este dispositivo.');
  };

  return {
    error,
    contact: (settlement) => void contact(settlement),
    complete: (settlement) => openRefund(settlement.bookingId),
    openBooking: (settlement) => openBooking(settlement.bookingId),
  };
}
