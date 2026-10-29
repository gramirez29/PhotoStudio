import { useState } from 'react';
import { initialContractValues, validateContract } from '../forms/contractForm';
import { goBack } from '../navigation/appNavigation';
import type { BookingResponse } from '../types/api/booking';
import type { ContractField, ContractFormErrors, ContractFormValues, ContractSignatureKind } from '../types/forms/contractForm.types';
import type { ContractFormState } from '../types/hooks/useContractForm.types';
import { bookingCommandErrorMessage, bookingLoadErrorMessage } from '../utils/apiErrors';
import { useBookingCommand } from './useBookingCommand';
import { useBookingContract } from './useBookingContract';

/**
 * State and actions of the screen that records the signature of a contract. When the client signs on the phone the screen shows
 * them the contract (the text the server holds, with the booking data filled in) and asks for what only they know; the signature
 * carries the version they read. A contract on paper only needs the name.
 * @param booking Booking whose contract is signed.
 * @returns The state the screen shows and the actions it calls.
 */
export function useContractForm(booking: BookingResponse): ContractFormState {
  const command = useBookingCommand(booking.id);
  const [values, setValues] = useState<ContractFormValues>(() => initialContractValues(booking.clientName));
  const [errors, setErrors] = useState<ContractFormErrors>({});
  const showsContract = values.kind === 'InPerson';
  const contract = useBookingContract(booking.id, showsContract);

  /**
   * Replaces one field and forgets its validation message. It works from the latest values, so two changes in a row do not undo each other.
   * @param field Field to change.
   * @param value New value of the field.
   */
  function change<Field extends ContractField>(field: Field, value: ContractFormValues[Field]): void {
    setValues((previous) => ({ ...previous, [field]: value }));
    setErrors((previous) => {
      const rest = { ...previous };
      delete rest[field];
      return rest;
    });
  }

  return {
    values,
    errors,
    submitError: command.isError ? bookingCommandErrorMessage(command.error) : null,
    isPending: command.isPending,
    showsContract,
    contractText: showsContract && contract.isSuccess ? contract.data.text : null,
    isContractLoading: showsContract && contract.isPending,
    contractError: showsContract && contract.isError ? bookingLoadErrorMessage(contract.error) : null,
    reloadContract: () => void contract.refetch(),
    setKind: (kind: ContractSignatureKind) => {
      setValues((previous) => ({ ...previous, kind }));
      setErrors({});
    },
    setText: (field, text) => change(field, text),
    setChoice: (field, value) => change(field, value),
    submit: () => {
      // On the phone, the version that travels is the one the client just read; without it there is nothing to sign.
      const version = contract.isSuccess ? contract.data.templateVersion : '';
      if (showsContract && version.length === 0) {
        return;
      }

      const result = validateContract(values, version);
      if (!result.ok) {
        setErrors(result.errors);
        return;
      }

      setErrors({});
      command.mutate({ kind: 'signContract', request: result.request }, { onSuccess: goBack });
    },
  };
}
