import { useLocalSearchParams } from 'expo-router';
import type { ReactElement } from 'react';
import { RescheduleForm } from '../../components/RescheduleForm';
import { ScreenLoader } from '../../components/ScreenLoader';
import { ScreenMessage } from '../../components/ScreenMessage';
import { useBooking } from '../../hooks/useBooking';
import { bookingLoadErrorMessage } from '../../utils/apiErrors';

/**
 * Screen to move a confirmed booking to another day or time. The booking comes from the cache of the detail screen.
 * @returns The screen.
 */
export default function RescheduleScreen(): ReactElement {
  const { id } = useLocalSearchParams<{ id: string }>();
  const bookingId = typeof id === 'string' ? id : '';
  const query = useBooking(bookingId);

  if (query.isPending) {
    return <ScreenLoader accessibilityLabel="Cargando reserva" />;
  }

  if (query.isError) {
    return <ScreenMessage message={bookingLoadErrorMessage(query.error)} />;
  }

  return <RescheduleForm booking={query.data} />;
}
