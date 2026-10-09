import { useState } from 'react';
import { validateRefund } from '../forms/refundForm';
import { goBack } from '../navigation/appNavigation';
import type { CompleteRefundRequest } from '../types/api/billing';
import type { RefundFormState } from '../types/hooks/useRefundForm.types';
import { refundErrorMessage } from '../utils/apiErrors';
import { useCompleteRefund } from './useCompleteRefund';

/**
 * State and actions of the refund screen: how the money was given back, an optional note, its validation and the request.
 * @param bookingId Booking whose refund is completed.
 * @returns The form state and its actions.
 */
export function useRefundForm(bookingId: string): RefundFormState {
  const complete = useCompleteRefund(bookingId);
  const [method, setMethod] = useState<CompleteRefundRequest['method']>('SinpeMovil');
  const [note, setNoteValue] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);

  return {
    method,
    note,
    validationError,
    submitError: complete.isError ? refundErrorMessage(complete.error) : null,
    isPending: complete.isPending,
    setMethod,
    setNote: (next: string) => {
      setNoteValue(next);
      setValidationError(null);
    },
    submit: () => {
      const result = validateRefund(method, note);
      if (!result.ok) {
        setValidationError(result.error);
        return;
      }

      setValidationError(null);
      complete.mutate(result.request, { onSuccess: goBack });
    },
  };
}
