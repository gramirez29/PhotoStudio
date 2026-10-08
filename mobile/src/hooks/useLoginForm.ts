import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { EMPTY_LOGIN_VALUES, validateLoginForm } from '../forms/loginForm';
import { signIn } from '../session/sessionManager';
import type { LoginField, LoginFormErrors, LoginFormValues } from '../types/forms/loginForm.types';
import type { LoginCredentials, LoginFormState } from '../types/hooks/useLoginForm.types';
import { loginErrorMessage } from '../utils/apiErrors';

/**
 * State and actions of the login screen: the typed values, their validation and the sign-in request. When the sign-in
 * succeeds the session store marks the app as signed in and the navigator replaces this screen by itself.
 * @returns The form state and its actions.
 */
export function useLoginForm(): LoginFormState {
  const [values, setValues] = useState<LoginFormValues>(EMPTY_LOGIN_VALUES);
  const [errors, setErrors] = useState<LoginFormErrors>({});
  // Not retried: a wrong password must count once, and the backend locks the account after a few failures.
  const login = useMutation({ mutationFn: ({ email, password }: LoginCredentials) => signIn(email, password) });

  return {
    values,
    errors,
    isPending: login.isPending,
    submitError: login.isError ? loginErrorMessage(login.error) : null,
    setField: (field: LoginField, text: string) => {
      setValues((previous) => ({ ...previous, [field]: text }));
      setErrors((previous) => {
        const next = { ...previous };
        delete next[field];
        return next;
      });
    },
    submit: () => {
      const result = validateLoginForm(values);
      if (!result.ok) {
        setErrors(result.errors);
        return;
      }

      setErrors({});
      login.mutate({ email: result.email, password: result.password });
    },
  };
}
