import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { Alert } from 'react-native';
import { accountApi } from '../api/client';
import { EMPTY_CHANGE_PASSWORD_VALUES, validateChangePasswordForm } from '../forms/changePasswordForm';
import { goBack } from '../navigation/appNavigation';
import { adoptSession } from '../session/sessionManager';
import type { ChangePasswordRequest } from '../types/api/auth';
import type {
  ChangePasswordField,
  ChangePasswordFormErrors,
  ChangePasswordFormValues,
} from '../types/forms/changePasswordForm.types';
import type { ChangePasswordFormState } from '../types/hooks/useChangePasswordForm.types';
import { changePasswordErrorMessage } from '../utils/apiErrors';

/**
 * State and actions of the change-password form: the typed values, their validation and the request. The backend closes
 * the other sessions and answers with the session of this device, which is adopted before leaving the screen, because the
 * refresh token the device had stops being valid.
 * @returns The form state and its actions.
 */
export function useChangePasswordForm(): ChangePasswordFormState {
  const [values, setValues] = useState<ChangePasswordFormValues>(EMPTY_CHANGE_PASSWORD_VALUES);
  const [errors, setErrors] = useState<ChangePasswordFormErrors>({});
  // Not retried: a wrong current password counts as a failed login, and the backend locks the account after a few.
  const change = useMutation({
    mutationFn: async (request: ChangePasswordRequest) => {
      await adoptSession(await accountApi.changePassword(request));
    },
    onSuccess: () => {
      Alert.alert('Contraseña actualizada', 'Se cerró la sesión en tus otros dispositivos.');
      goBack();
    },
  });

  return {
    values,
    errors,
    isPending: change.isPending,
    submitError: change.isError ? changePasswordErrorMessage(change.error) : null,
    setField: (field: ChangePasswordField, text: string) => {
      setValues((previous) => ({ ...previous, [field]: text }));
      setErrors((previous) => {
        const next = { ...previous };
        delete next[field];
        return next;
      });
    },
    submit: () => {
      const result = validateChangePasswordForm(values);
      if (!result.ok) {
        setErrors(result.errors);
        return;
      }

      setErrors({});
      change.mutate(result.request);
    },
  };
}
