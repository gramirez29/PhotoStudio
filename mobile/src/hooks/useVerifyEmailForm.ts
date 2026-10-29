import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { accountApi } from '../api/client';
import { formatCodeInput, validateVerifyEmailForm } from '../forms/verifyEmailForm';
import { adoptSession } from '../session/sessionManager';
import { useSessionStore } from '../session/sessionStore';
import type { VerifyEmailRequest } from '../types/api/auth';
import type { VerifyEmailFormState } from '../types/hooks/useVerifyEmailForm.types';
import { resendEmailCodeErrorMessage, verifyEmailErrorMessage } from '../utils/apiErrors';
import { maskEmail } from '../utils/email';
import { useCooldown } from './useCooldown';

/** Seconds the backend makes the user wait between two codes. */
const RESEND_COOLDOWN_SECONDS = 60;

/**
 * Picks the message of whichever request failed last, if any.
 * @param verifyError Error of the check of the code, if it failed.
 * @param resendError Error of the shipment of a new code, if it failed.
 * @returns The message, or null when nothing failed.
 */
function failureMessage(verifyError: Error | null, resendError: Error | null): string | null {
  if (verifyError !== null) {
    return verifyEmailErrorMessage(verifyError);
  }

  return resendError === null ? null : resendEmailCodeErrorMessage(resendError);
}

/**
 * State and actions of the screen that verifies the email: the code typed, checking it, and asking for a new one. Verifying
 * changes what the token can do, so the new session the backend answers with replaces the one of the device and the app opens.
 * @returns The state the screen shows and the actions it calls.
 */
export function useVerifyEmailForm(): VerifyEmailFormState {
  const email = useSessionStore((state) => state.session?.email ?? '');
  const [code, setCodeState] = useState('');
  const [codeError, setCodeError] = useState<string | undefined>(undefined);
  const [notice, setNotice] = useState<string | null>(null);
  const cooldown = useCooldown();

  // Not retried: every wrong code counts against the limit, and a repeated shipment sends another email.
  const verifyMutation = useMutation({
    mutationFn: async (request: VerifyEmailRequest) => {
      await adoptSession(await accountApi.verifyEmail(request));
    },
  });
  const resendMutation = useMutation({
    mutationFn: () => accountApi.resendEmailVerification(),
    onSuccess: () => {
      setNotice(`Te enviamos un código nuevo a ${maskEmail(email)}.`);
      cooldown.start(RESEND_COOLDOWN_SECONDS);
    },
  });

  return {
    maskedEmail: maskEmail(email),
    code,
    codeError,
    isVerifying: verifyMutation.isPending,
    isSending: resendMutation.isPending,
    submitError: failureMessage(verifyMutation.error, resendMutation.error),
    notice,
    cooldownSeconds: cooldown.remaining,
    setCode: (text) => {
      setCodeState(formatCodeInput(text));
      setCodeError(undefined);
    },
    verify: () => {
      const result = validateVerifyEmailForm(code);
      if (!result.ok) {
        setCodeError(result.error);
        return;
      }

      setNotice(null);
      resendMutation.reset();
      verifyMutation.mutate(result.request);
    },
    resend: () => {
      setNotice(null);
      verifyMutation.reset();
      resendMutation.mutate();
    },
  };
}
