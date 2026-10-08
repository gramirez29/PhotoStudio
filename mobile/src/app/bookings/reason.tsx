import { useLocalSearchParams } from 'expo-router';
import type { ReactElement } from 'react';
import { ReasonForm } from '../../components/ReasonForm';
import { ScreenLoader } from '../../components/ScreenLoader';
import { ScreenMessage } from '../../components/ScreenMessage';
import { useBooking } from '../../hooks/useBooking';
import { bookingLoadErrorMessage } from '../../utils/apiErrors';
import { parseReasonAction } from '../../utils/bookingActions';

/**
 * Screen to type the reason of an action on a booking (cancel it or revert its absence mark). The booking comes from the
 * cache of the detail screen.
 * @returns The screen.
 */
export default function ReasonScreen(): ReactElement {
  const { id, action } = useLocalSearchParams<{ id: string; action: string }>();
  const bookingId = typeof id === 'string' ? id : '';
  const reasonAction = parseReasonAction(action);
  const query = useBooking(bookingId);

  if (reasonAction === null) {
    return <ScreenMessage message="Esta acción no existe. Vuelve atrás e inténtalo de nuevo." />;
  }

  if (query.isPending) {
    return <ScreenLoader accessibilityLabel="Cargando reserva" />;
  }

  if (query.isError) {
    return <ScreenMessage message={bookingLoadErrorMessage(query.error)} />;
  }

  return <ReasonForm booking={query.data} action={reasonAction} />;
}
