import { useLocalSearchParams } from 'expo-router';
import type { ReactElement } from 'react';
import { RefundForm } from '../../components/RefundForm';
import { ScreenLoader } from '../../components/ScreenLoader';
import { ScreenMessage } from '../../components/ScreenMessage';
import { useSettlement } from '../../hooks/useSettlement';

/**
 * Screen to record that the refund of a booking was given back to the client. The settlement comes from the cache of the
 * screen that opened it.
 * @returns The screen.
 */
export default function RefundScreen(): ReactElement {
  const { id } = useLocalSearchParams<{ id: string }>();
  const bookingId = typeof id === 'string' ? id : '';
  const query = useSettlement(bookingId, true);

  if (query.isPending) {
    return <ScreenLoader accessibilityLabel="Cargando reembolso" />;
  }

  if (query.isError) {
    return <ScreenMessage message="No se pudo cargar el reembolso. Revisa la conexión e inténtalo de nuevo." />;
  }

  if (query.data === null || query.data.refundStatus !== 'Pending') {
    return <ScreenMessage message="Este reembolso ya no está pendiente." />;
  }

  return <RefundForm settlement={query.data} />;
}
