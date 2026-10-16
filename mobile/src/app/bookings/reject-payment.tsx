import { useLocalSearchParams } from 'expo-router';
import type { ReactElement } from 'react';
import { RejectPaymentForm } from '../../components/RejectPaymentForm';
import { ScreenLoader } from '../../components/ScreenLoader';
import { ScreenMessage } from '../../components/ScreenMessage';
import { useBooking } from '../../hooks/useBooking';
import { bookingLoadErrorMessage } from '../../utils/apiErrors';

/**
 * Screen to type the reason for rejecting a payment proof. The booking comes from the cache of the detail screen, and the
 * proof must still be waiting for verification.
 * @returns The screen.
 */
export default function RejectPaymentScreen(): ReactElement {
  const { id, paymentId } = useLocalSearchParams<{ id: string; paymentId: string }>();
  const bookingId = typeof id === 'string' ? id : '';
  const query = useBooking(bookingId);

  if (query.isPending) {
    return <ScreenLoader accessibilityLabel="Cargando reserva" />;
  }

  if (query.isError) {
    return <ScreenMessage message={bookingLoadErrorMessage(query.error)} />;
  }

  const payment = query.data.payments.find((candidate) => candidate.id === paymentId);
  if (payment === undefined || payment.status !== 'PendingVerification') {
    return <ScreenMessage message="Este comprobante ya no está pendiente. Vuelve a la reserva para ver su estado." />;
  }

  return <RejectPaymentForm booking={query.data} payment={payment} />;
}
