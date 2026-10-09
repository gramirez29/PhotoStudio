import { useLocalSearchParams } from 'expo-router';
import type { ReactElement } from 'react';
import { ContractForm } from '../../components/ContractForm';
import { ScreenLoader } from '../../components/ScreenLoader';
import { ScreenMessage } from '../../components/ScreenMessage';
import { useBooking } from '../../hooks/useBooking';
import { bookingLoadErrorMessage } from '../../utils/apiErrors';

/**
 * Screen to record the signature of the contract of a booking, in person or on paper. The booking comes from the cache of
 * the detail screen.
 * @returns The screen.
 */
export default function ContractScreen(): ReactElement {
  const { id } = useLocalSearchParams<{ id: string }>();
  const bookingId = typeof id === 'string' ? id : '';
  const query = useBooking(bookingId);

  if (query.isPending) {
    return <ScreenLoader accessibilityLabel="Cargando reserva" />;
  }

  if (query.isError) {
    return <ScreenMessage message={bookingLoadErrorMessage(query.error)} />;
  }

  return <ContractForm booking={query.data} />;
}
