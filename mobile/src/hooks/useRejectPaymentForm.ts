import { useState } from 'react';
import { validateRejectReason } from '../forms/rejectPaymentForm';
import { goBack } from '../navigation/appNavigation';
import type { RejectPaymentFormState } from '../types/hooks/useRejectPaymentForm.types';
import { bookingCommandErrorMessage } from '../utils/apiErrors';
import { useBookingCommand } from './useBookingCommand';

/**
 * State and actions of the screen that rejects a payment proof: the typed reason, its validation and the request.
 * @param bookingId Booking the proof belongs to.
 * @param paymentId Proof being rejected.
 * @returns The form state and its actions.
 */
export function useRejectPaymentForm(bookingId: string, paymentId: string): RejectPaymentFormState {
  const command = useBookingCommand(bookingId);
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
      const result = validateRejectReason(text);
      if (!result.ok) {
        setValidationError(result.error);
        return;
      }

      setValidationError(null);
      command.mutate({ kind: 'rejectPayment', paymentId, reason: result.reason }, { onSuccess: goBack });
    },
  };
}
