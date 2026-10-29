import { useState } from 'react';
import { initialEditValues, validateEditBookingForm } from '../forms/editBookingForm';
import { goBack } from '../navigation/appNavigation';
import type { BookingResponse } from '../types/api/booking';
import type { EditBookingField, EditBookingFormErrors, EditBookingFormValues } from '../types/forms/editBookingForm.types';
import type { EditBookingFormState } from '../types/hooks/useEditBookingForm.types';
import { editBookingErrorMessage } from '../utils/apiErrors';
import { formatPhoneInput } from '../utils/phone';
import { computeTotals, findPackageByLabel, priceAfterPackageChange } from '../utils/pricing';
import { useBookingCommand } from './useBookingCommand';

/**
 * State and actions of the screen that corrects a booking. It starts with what the booking has now. Whether the package and the
 * price can still change is not decided here: the backend offers the action `EditPackage` while it is possible.
 * @param booking Booking being corrected.
 * @returns The state the screen shows and the actions it calls.
 */
export function useEditBookingForm(booking: BookingResponse): EditBookingFormState {
  const command = useBookingCommand(booking.id);
  const [values, setValues] = useState<EditBookingFormValues>(() => initialEditValues(booking));
  const [errors, setErrors] = useState<EditBookingFormErrors>({});
  const packageEditable = booking.allowedActions.includes('EditPackage');

  /**
   * Forgets the validation message of a field the photographer is correcting.
   * @param field Field being edited.
   */
  function clearError(field: EditBookingField): void {
    setErrors((previous) => {
      const next = { ...previous };
      delete next[field];
      return next;
    });
  }

  return {
    values,
    errors,
    isPending: command.isPending,
    submitError: command.isError ? editBookingErrorMessage(command.error) : null,
    packageEditable,
    totals: computeTotals(values.price, values.extras, values.discount),
    setText: (field, text) => {
      setValues((previous) => ({ ...previous, [field]: text }));
      clearError(field);
    },
    setPhone: (text) => {
      setValues((previous) => ({ ...previous, clientPhone: formatPhoneInput(text) }));
      clearError('clientPhone');
    },
    setPackage: (packageName) => {
      setValues((previous) => ({ ...previous, packageName }));
      clearError('packageName');
    },
    setPackageTier: (label) => {
      const option = findPackageByLabel(label);
      if (option === undefined) {
        return;
      }

      setValues((previous) => ({
        ...previous,
        packageTier: option.tier,
        price: priceAfterPackageChange(previous.price, previous.packageTier, option),
      }));
      clearError('packageTier');
      clearError('price');
    },
    setExtras: (extras) => {
      setValues((previous) => ({ ...previous, extras }));
      clearError('discount');
    },
    submit: () => {
      const result = validateEditBookingForm(values, booking, packageEditable);
      if (!result.ok) {
        setErrors(result.errors);
        return;
      }

      setErrors({});
      command.mutate({ kind: 'editDetails', request: result.request }, { onSuccess: goBack });
    },
  };
}
