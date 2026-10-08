import { useState } from 'react';
import { validateReason } from '../forms/reasonForm';
import { goBack } from '../navigation/appNavigation';
import type { BookingResponse } from '../types/api/booking';
import type { BookingCommand } from '../types/hooks/useBookingCommand.types';
import type { ReasonFormState } from '../types/hooks/useReasonForm.types';
import type { ReasonAction } from '../types/utils/bookingActions.types';
import { bookingCommandErrorMessage } from '../utils/apiErrors';
import { useBookingCommand } from './useBookingCommand';

/**
 * State and actions of the reason screen: the typed reason, its validation and the request.
 * @param booking Booking the action is performed on.
 * @param action Action that needs a reason.
 * @returns The form state and its actions.
 */
export function useReasonForm(booking: BookingResponse, action: ReasonAction): ReasonFormState {
  const command = useBookingCommand(booking.id);
  const [text, setTextValue] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);

  return {
    text,
    validationError,
    submitError: command.isError ? bookingCommandErrorMessage(command.error) : null,
    isPending: command.isPending,
    setText: (next: string) => {
      setTextValue(next);
      setValidationError(null);
    },
    submit: () => {
      const result = validateReason(action, booking.status, text);
      if (!result.ok) {
        setValidationError(result.error);
        return;
      }

      setValidationError(null);
      // Reverting always requires a reason, so validation never leaves it null there; the backend would reject an empty one.
      const toSend: BookingCommand =
        action === 'Cancel'
          ? { kind: 'cancel', reason: result.reason }
          : { kind: 'revertClientAbsent', reason: result.reason ?? '' };
      command.mutate(toSend, { onSuccess: goBack });
    },
  };
}
