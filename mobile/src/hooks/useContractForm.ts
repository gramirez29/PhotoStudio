import { useState } from 'react';
import { validateContract } from '../forms/contractForm';
import { goBack } from '../navigation/appNavigation';
import type { BookingResponse } from '../types/api/booking';
import type { ContractSignatureKind } from '../types/forms/contractForm.types';
import type { ContractFormState } from '../types/hooks/useContractForm.types';
import { bookingCommandErrorMessage } from '../utils/apiErrors';
import { useBookingCommand } from './useBookingCommand';

/**
 * State and actions of the contract screen: who signs, how, its validation and the request. The name starts as the name of
 * the client, which is who normally signs.
 * @param booking Booking whose contract is signed.
 * @returns The form state and its actions.
 */
export function useContractForm(booking: BookingResponse): ContractFormState {
  const command = useBookingCommand(booking.id);
  const [signerName, setSignerNameValue] = useState(booking.clientName);
  const [kind, setKind] = useState<ContractSignatureKind>('InPerson');
  const [validationError, setValidationError] = useState<string | null>(null);

  return {
    signerName,
    kind,
    validationError,
    submitError: command.isError ? bookingCommandErrorMessage(command.error) : null,
    isPending: command.isPending,
    setSignerName: (next: string) => {
      setSignerNameValue(next);
      setValidationError(null);
    },
    setKind,
    submit: () => {
      const result = validateContract(signerName, kind);
      if (!result.ok) {
        setValidationError(result.error);
        return;
      }

      setValidationError(null);
      command.mutate({ kind: 'signContract', request: result.request }, { onSuccess: goBack });
    },
  };
}
