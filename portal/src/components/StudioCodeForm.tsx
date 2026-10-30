import { useEffect, useState, type FormEvent, type ReactElement } from 'react';
import { useCountdown } from '../hooks/useCountdown';
import { useStudioResendCode, useStudioVerifyCode } from '../hooks/useStudioMutations';
import type { StudioCodeFormProps } from '../types/components/StudioComponents.types';
import type { ActionErrorView } from '../types/utils/apiErrors.types';
import { clearPendingCode } from '../utils/pendingCode';
import { describeCodeError } from '../utils/studioErrors';
import { CheckboxField } from './CheckboxField';
import { StepHeading } from './StepHeading';
import { TextField } from './TextField';

/** Digits of the code. */
const CODE_LENGTH = 6;

/** Seconds the server makes the person wait between two codes. */
const RESEND_COOLDOWN_SECONDS = 60;

/**
 * The code step of the web area: the photographer types the code from the email. Only digits are accepted, the phone offers its
 * numeric keyboard and may autofill the code from the message. The browser can be remembered for 30 days, which only skips this
 * step, never the password; it is off by default because it is a decision about the computer, and the page cannot know whether
 * the computer is theirs.
 * @param props Component props.
 * @returns The form.
 */
export function StudioCodeForm({ maskedEmail, onCancel }: StudioCodeFormProps): ReactElement {
  const verify = useStudioVerifyCode();
  const resend = useStudioResendCode();
  const wait = useCountdown();
  const [code, setCode] = useState('');
  const [remember, setRemember] = useState(false);
  const [error, setError] = useState<ActionErrorView | null>(null);
  const [resent, setResent] = useState(false);

  // The code was just sent when this step appears, so the server will refuse another one for a minute.
  const startWait = wait.start;
  useEffect(() => {
    startWait(RESEND_COOLDOWN_SECONDS);
  }, [startWait]);

  /**
   * Keeps only the digits the person typed or pasted.
   * @param value Text of the field.
   */
  function changeCode(value: string): void {
    setCode(value.replace(/\D/g, '').slice(0, CODE_LENGTH));
    setError(null);
  }

  /**
   * Checks the code with the server.
   * @param event Submit event of the form.
   */
  function submit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    if (code.length !== CODE_LENGTH) {
      setError({ message: `Escribe los ${CODE_LENGTH} dígitos del código.`, retryAfterSeconds: null, sessionExpired: false });
      return;
    }

    setError(null);
    verify.mutate({ code, rememberDevice: remember }, { onError: (failure) => setError(describeCodeError(failure)) });
  }

  /** Asks for another code and restarts the wait. */
  function askAgain(): void {
    setError(null);
    setResent(false);
    resend.mutate(undefined, {
      onSuccess: () => {
        setCode('');
        setResent(true);
        wait.start(RESEND_COOLDOWN_SECONDS);
      },
      onError: (failure) => {
        const view = describeCodeError(failure);
        setError(view);
        wait.start(view.retryAfterSeconds ?? RESEND_COOLDOWN_SECONDS);
      },
    });
  }

  /** Goes back to the password and forgets that a code was waiting. */
  function cancel(): void {
    clearPendingCode();
    onCancel();
  }

  return (
    <section className="card" aria-labelledby="proof-step-title">
      <StepHeading>Escribe el código</StepHeading>
      <p>
        Te lo enviamos a <strong>{maskedEmail}</strong>. Vence en 10 minutos.
      </p>

      <form className="stack" onSubmit={submit} noValidate>
        <TextField
          id="code"
          label="Código de 6 dígitos"
          value={code}
          onChange={changeCode}
          error={error?.message}
          inputMode="numeric"
          autoComplete="one-time-code"
          maxLength={CODE_LENGTH}
          autoFocus
          placeholder="000000"
        />
        <CheckboxField id="remember" checked={remember} onChange={setRemember}>
          Recordar este navegador por 30 días: aquí no me pedirás el código (la contraseña sí). No lo marques en una computadora que no sea tuya.
        </CheckboxField>
        <p className="visually-hidden" role="status">
          {resent ? 'Te enviamos un código nuevo.' : ''}
        </p>

        <div className="actions">
          <button type="submit" className="button" disabled={verify.isPending}>
            {verify.isPending ? 'Verificando…' : 'Verificar'}
          </button>
          <button type="button" className="button button--secondary" disabled={resend.isPending || wait.remaining > 0} onClick={askAgain}>
            {wait.remaining > 0 ? `Reenviar en ${wait.remaining} s` : 'Reenviar código'}
          </button>
          <button type="button" className="button button--secondary" onClick={cancel}>
            Volver
          </button>
        </div>
      </form>
    </section>
  );
}
