import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { EMPTY_REGISTER_VALUES, validateRegisterForm } from '../forms/registerForm';
import { register } from '../session/sessionManager';
import type { RegisterRequest } from '../types/api/auth';
import type { RegisterField, RegisterFormErrors, RegisterFormValues } from '../types/forms/registerForm.types';
import type { RegisterFormState } from '../types/hooks/useRegisterForm.types';
import { registerErrorMessage } from '../utils/apiErrors';
import { formatPhoneInput } from '../utils/phone';

/**
 * State and actions of the account creation form: the typed values, their validation and the request. When the account is
 * created the session store marks the app as signed in and the navigator replaces the screen by itself.
 * @returns The form state and its actions.
 */
export function useRegisterForm(): RegisterFormState {
  const [values, setValues] = useState<RegisterFormValues>(EMPTY_REGISTER_VALUES);
  const [errors, setErrors] = useState<RegisterFormErrors>({});
  // Not retried: creating an account is not safe to repeat blindly, and the backend limits how many can be created.
  const creation = useMutation({ mutationFn: (request: RegisterRequest) => register(request) });

  /**
   * Clears the validation message of a field once the user edits it.
   * @param field Field being edited.
   */
  function clearError(field: RegisterField): void {
    setErrors((previous) => {
      const next = { ...previous };
      delete next[field];
      return next;
    });
  }

  return {
    values,
    errors,
    isPending: creation.isPending,
    submitError: creation.isError ? registerErrorMessage(creation.error) : null,
    setField: (field: RegisterField, text: string) => {
      setValues((previous) => ({ ...previous, [field]: text }));
      clearError(field);
    },
    setPhone: (text: string) => {
      setValues((previous) => ({ ...previous, phone: formatPhoneInput(text) }));
      clearError('phone');
    },
    submit: () => {
      const result = validateRegisterForm(values);
      if (!result.ok) {
        setErrors(result.errors);
        return;
      }

      setErrors({});
      creation.mutate(result.request);
    },
  };
}
