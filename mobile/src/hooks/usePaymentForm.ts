import { useState } from 'react';
import { validatePayment } from '../forms/paymentForm';
import { goBack } from '../navigation/appNavigation';
import type { BookingResponse, RecordInPersonPaymentRequest } from '../types/api/booking';
import type { PaymentFormState } from '../types/hooks/usePaymentForm.types';
import { bookingCommandErrorMessage } from '../utils/apiErrors';
import { createIdempotencyKey } from '../utils/idempotency';
import { suggestedPaymentAmount } from '../utils/payments';
import { useBookingCommand } from './useBookingCommand';

/**
 * State and actions of the payment screen: the amount, the method, its validation and the request. The idempotency key is
 * created once when the screen opens, so a double tap or a retry after a lost answer records the payment only once.
 * @param booking Booking the payment is recorded on.
 * @returns The form state and its actions.
 */
export function usePaymentForm(booking: BookingResponse): PaymentFormState {
  const command = useBookingCommand(booking.id);
  const [amount, setAmountValue] = useState(() => suggestedPaymentAmount(booking));
  const [method, setMethod] = useState<RecordInPersonPaymentRequest['method']>('Cash');
  const [idempotencyKey] = useState(() => createIdempotencyKey());
  const [validationError, setValidationError] = useState<string | null>(null);

  return {
    amount,
    method,
    validationError,
    submitError: command.isError ? bookingCommandErrorMessage(command.error) : null,
    isPending: command.isPending,
    setAmount: (next: string) => {
      setAmountValue(next);
      setValidationError(null);
    },
    setMethod,
    submit: () => {
      const result = validatePayment(booking, { amount, method, idempotencyKey });
      if (!result.ok) {
        setValidationError(result.error);
        return;
      }

      setValidationError(null);
      command.mutate({ kind: 'recordPayment', request: result.request }, { onSuccess: goBack });
    },
  };
}
