import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { authApi } from '../api/authClient';
import {
  EMPTY_RECOVER_PASSWORD_VALUES,
  validateConfirmResetForm,
  validateRequestResetForm,
} from '../forms/recoverPasswordForm';
import { formatCodeInput } from '../forms/verifyEmailForm';
import type { PasswordResetConfirmRequest, PasswordResetRequest } from '../types/api/auth';
import type {
  RecoverPasswordField,
  RecoverPasswordFormErrors,
  RecoverPasswordFormValues,
} from '../types/forms/recoverPasswordForm.types';
import type { RecoverPasswordFormState, RecoverPasswordStep } from '../types/hooks/useRecoverPasswordForm.types';
import { confirmPasswordResetErrorMessage, requestPasswordResetErrorMessage } from '../utils/apiErrors';
import { useCooldown } from './useCooldown';

/** Seconds the user waits before asking for another code, so the screen does not invite a request the backend would ignore. */
const RESEND_COOLDOWN_SECONDS = 60;

/**
 * What the screen says after asking for a code. It does not say whether the account exists, because the backend does not.
 */
const CODE_REQUESTED_NOTICE =
  'Si hay una cuenta con ese usuario o correo, y su correo está verificado, te enviamos un código. Puede tardar un minuto; revisa también el correo no deseado.';

/**
 * State and actions of the recovery of a forgotten password. First the user names the account and asks for a code; then they type
 * the code and the new password. Nobody is signed in by recovering: when it works, the screen goes back to the login.
 * @param onRecovered Called once the password was changed.
 * @returns The state the screen shows and the actions it calls.
 */
export function useRecoverPasswordForm(onRecovered: () => void): RecoverPasswordFormState {
  const [step, setStep] = useState<RecoverPasswordStep>('request');
  const [values, setValues] = useState<RecoverPasswordFormValues>(EMPTY_RECOVER_PASSWORD_VALUES);
  const [errors, setErrors] = useState<RecoverPasswordFormErrors>({});
  const [notice, setNotice] = useState<string | null>(null);
  const cooldown = useCooldown();

  // Neither is retried: a repeated request sends another email, and every wrong code counts against the limit.
  const requestMutation = useMutation({
    mutationFn: (request: PasswordResetRequest) => authApi.requestPasswordReset(request),
    onSuccess: () => {
      setStep('confirm');
      setNotice(CODE_REQUESTED_NOTICE);
      cooldown.start(RESEND_COOLDOWN_SECONDS);
    },
  });
  const confirmMutation = useMutation({
    mutationFn: (request: PasswordResetConfirmRequest) => authApi.confirmPasswordReset(request),
    onSuccess: onRecovered,
  });

  let submitError: string | null = null;
  if (confirmMutation.isError) {
    submitError = confirmPasswordResetErrorMessage(confirmMutation.error);
  } else if (requestMutation.isError) {
    submitError = requestPasswordResetErrorMessage(requestMutation.error);
  }

  return {
    step,
    values,
    errors,
    isPending: requestMutation.isPending || confirmMutation.isPending,
    submitError,
    notice,
    cooldownSeconds: cooldown.remaining,
    setField: (field: RecoverPasswordField, text: string) => {
      setValues((previous) => ({ ...previous, [field]: field === 'code' ? formatCodeInput(text) : text }));
      setErrors((previous) => {
        const next = { ...previous };
        delete next[field];
        return next;
      });
    },
    requestCode: () => {
      const result = validateRequestResetForm(values.identifier);
      if (!result.ok) {
        setErrors(result.errors);
        return;
      }

      setErrors({});
      setNotice(null);
      confirmMutation.reset();
      requestMutation.mutate(result.request);
    },
    confirm: () => {
      const result = validateConfirmResetForm(values.identifier, values);
      if (!result.ok) {
        setErrors(result.errors);
        return;
      }

      setErrors({});
      requestMutation.reset();
      confirmMutation.mutate(result.request);
    },
    resendCode: () => {
      setNotice(null);
      confirmMutation.reset();
      requestMutation.mutate({ identifier: values.identifier.trim() });
    },
    changeAccount: () => {
      setStep('request');
      setValues((previous) => ({ ...EMPTY_RECOVER_PASSWORD_VALUES, identifier: previous.identifier }));
      setErrors({});
      setNotice(null);
      requestMutation.reset();
      confirmMutation.reset();
    },
  };
}
