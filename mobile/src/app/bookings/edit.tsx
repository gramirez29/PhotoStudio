import { useLocalSearchParams } from 'expo-router';
import type { ReactElement } from 'react';
import { EditBookingForm } from '../../components/EditBookingForm';
import { ScreenLoader } from '../../components/ScreenLoader';
import { ScreenMessage } from '../../components/ScreenMessage';
import { useBooking } from '../../hooks/useBooking';
import { bookingLoadErrorMessage } from '../../utils/apiErrors';

/**
 * Screen that corrects a booking. It loads the booking and shows the form, or says why it cannot.
 * @returns The screen.
 */
export default function EditBookingScreen(): ReactElement {
  const { id } = useLocalSearchParams<{ id: string }>();
  const bookingId = typeof id === 'string' ? id : '';
  const query = useBooking(bookingId);

  if (query.isPending) {
    return <ScreenLoader accessibilityLabel="Cargando reserva" />;
  }

  if (query.isError) {
    return <ScreenMessage message={bookingLoadErrorMessage(query.error)} />;
  }

  return <EditBookingForm booking={query.data} />;
}
