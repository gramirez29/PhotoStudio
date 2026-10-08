import { useState } from 'react';
import { env } from '../config/env';
import { initialFormValues, validateCreateBookingForm } from '../forms/createBookingForm';
import type {
  CreateBookingField,
  CreateBookingFormErrors,
  CreateBookingFormValues,
  CreateBookingTextField,
} from '../types/forms/createBookingForm.types';
import { replaceWithBooking } from '../navigation/appNavigation';
import type { NewBookingFormState } from '../types/hooks/useNewBookingForm.types';
import { createBookingErrorMessage } from '../utils/apiErrors';
import { formatPhoneInput } from '../utils/phone';
import { useCreateBooking } from './useCreateBooking';

/**
 * State and actions of the create-booking screen: the typed values, their validation and the request.
 * @returns The form state and its actions.
 */
export function useNewBookingForm(): NewBookingFormState {
  const createBooking = useCreateBooking();
  const [today] = useState(() => new Date());
  const [values, setValues] = useState<CreateBookingFormValues>(() => initialFormValues(today));
  const [errors, setErrors] = useState<CreateBookingFormErrors>({});

  /**
   * Clears the validation message of a field once the photographer edits it.
   * @param field Field being edited.
   */
  function clearError(field: CreateBookingField): void {
    setErrors((previous) => {
      const next = { ...previous };
      delete next[field];
      return next;
    });
  }

  return {
    today,
    values,
    errors,
    isPending: createBooking.isPending,
    submitError: createBooking.isError ? createBookingErrorMessage(createBooking.error) : null,
    setText: (field: CreateBookingTextField, text: string) => {
      setValues((previous) => ({ ...previous, [field]: text }));
      clearError(field);
    },
    setPhone: (text: string) => {
      setValues((previous) => ({ ...previous, clientPhone: formatPhoneInput(text) }));
      clearError('clientPhone');
    },
    setPackage: (packageName: string) => {
      setValues((previous) => ({ ...previous, packageName }));
      clearError('packageName');
    },
    setStart: (start: Date) => {
      setValues((previous) => ({ ...previous, start }));
      clearError('start');
    },
    setDuration: (hours: number) => setValues((previous) => ({ ...previous, durationHours: hours })),
    submit: () => {
      if (env.photographerId === null) {
        return;
      }

      const result = validateCreateBookingForm(values, env.photographerId, new Date());
      if (!result.ok) {
        setErrors(result.errors);
        return;
      }

      setErrors({});
      createBooking.mutate(result.request, { onSuccess: (booking) => replaceWithBooking(booking.id) });
    },
  };
}
