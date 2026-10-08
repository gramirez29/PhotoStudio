import { useState } from 'react';
import { validateReschedule } from '../forms/rescheduleBookingForm';
import { goBack } from '../navigation/appNavigation';
import type { BookingResponse } from '../types/api/booking';
import type { RescheduleFormState } from '../types/hooks/useRescheduleForm.types';
import { rescheduleBookingErrorMessage } from '../utils/apiErrors';
import { useRescheduleBooking } from './useRescheduleBooking';

/**
 * State and actions of the reschedule screen: the chosen date, its validation and the request.
 * @param booking Booking being moved.
 * @returns The form state and its actions.
 */
export function useRescheduleForm(booking: BookingResponse): RescheduleFormState {
  const reschedule = useRescheduleBooking(booking.id);
  const [today] = useState(() => new Date());
  const [newStart, setNewStartValue] = useState(() => new Date(booking.sessionStart));
  const [validationError, setValidationError] = useState<string | null>(null);

  return {
    today,
    newStart,
    validationError,
    submitError: reschedule.isError ? rescheduleBookingErrorMessage(reschedule.error) : null,
    isPending: reschedule.isPending,
    setNewStart: (next: Date) => {
      setNewStartValue(next);
      setValidationError(null);
    },
    submit: () => {
      const result = validateReschedule(booking, newStart, new Date());
      if (!result.ok) {
        setValidationError(result.error);
        return;
      }

      setValidationError(null);
      reschedule.mutate(result.request, { onSuccess: goBack });
    },
  };
}
