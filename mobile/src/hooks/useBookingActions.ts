import { Alert } from 'react-native';
import { openContract, openPayment, openReason, openReschedule } from '../navigation/appNavigation';
import type { BookingResponse } from '../types/api/booking';
import type { BookingActionsState } from '../types/hooks/useBookingActions.types';
import type { BookingCommand } from '../types/hooks/useBookingCommand.types';
import { bookingCommandErrorMessage } from '../utils/apiErrors';
import { confirmationFor } from '../utils/bookingActions';
import { useBookingCommand } from './useBookingCommand';

/**
 * Handlers of the actions shown in the booking detail. Actions that cannot be undone from a second screen ask for
 * confirmation first; the ones that need a reason open their own form.
 * @param booking Booking whose actions are handled.
 * @returns The handlers and the state of the request.
 */
export function useBookingActions(booking: BookingResponse): BookingActionsState {
  const command = useBookingCommand(booking.id);

  /**
   * Shows the confirmation dialog of an action and sends it when the photographer accepts.
   * @param action Action to confirm.
   * @param toSend Command sent when the photographer confirms.
   */
  function confirmAndSend(action: 'Complete' | 'MarkClientAbsent', toSend: BookingCommand): void {
    const texts = confirmationFor(action, booking.clientName);
    Alert.alert(texts.title, texts.message, [
      { text: 'Volver', style: 'cancel' },
      {
        text: texts.confirmLabel,
        style: action === 'MarkClientAbsent' ? 'destructive' : 'default',
        onPress: () => command.mutate(toSend),
      },
    ]);
  }

  return {
    isPending: command.isPending,
    errorMessage: command.isError ? bookingCommandErrorMessage(command.error) : null,
    signContract: () => openContract(booking.id),
    recordPayment: () => openPayment(booking.id),
    reschedule: () => openReschedule(booking.id),
    cancel: () => openReason(booking.id, 'Cancel'),
    complete: () => confirmAndSend('Complete', { kind: 'complete' }),
    markClientAbsent: () => confirmAndSend('MarkClientAbsent', { kind: 'markClientAbsent' }),
    revertClientAbsent: () => openReason(booking.id, 'RevertClientAbsent'),
  };
}
