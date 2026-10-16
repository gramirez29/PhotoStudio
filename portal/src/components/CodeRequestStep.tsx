import { useState, type ReactElement } from 'react';
import { useCountdown } from '../hooks/useCountdown';
import { useRequestCode } from '../hooks/useProofMutations';
import type { CodeRequestStepProps } from '../types/components/CodeRequestStep.types';
import type { ActionErrorView } from '../types/utils/apiErrors.types';
import { describeCodeRequestError } from '../utils/apiErrors';
import { StepHeading } from './StepHeading';

/**
 * First step of the proof flow: explains why a code is needed and sends it to the email the photographer registered. The
 * client never types an address, so holding the link is not enough to receive the code somewhere else.
 * @param props Component props.
 * @returns The step.
 */
export function CodeRequestStep({ token, notice, onSent, onCancel }: CodeRequestStepProps): ReactElement {
  const request = useRequestCode(token);
  const wait = useCountdown();
  const [error, setError] = useState<ActionErrorView | null>(null);

  /** Asks for the code; on success the flow moves on, on failure the reason is shown and, if the server said so, the wait starts. */
  function send(): void {
    setError(null);
    request.mutate(undefined, {
      onSuccess: (sent) => onSent(sent.maskedEmail),
      onError: (failure) => {
        const view = describeCodeRequestError(failure);
        setError(view);
        if (view.retryAfterSeconds !== null) {
          wait.start(view.retryAfterSeconds);
        }
      },
    });
  }

  const waiting = wait.remaining > 0;

  return (
    <section className="card" aria-labelledby="proof-step-title">
      <StepHeading>Confirma que eres tú</StepHeading>
      <p>
        Para subir tu comprobante te enviaremos un código de 6 dígitos al correo que tu fotógrafo registró para esta reserva.
      </p>

      {notice !== null && <p className="notice">{notice}</p>}

      {error !== null && (
        <p className="field__error" role="alert">
          {error.message}
        </p>
      )}

      <div className="actions">
        <button type="button" className="button" disabled={request.isPending || waiting} onClick={send}>
          {request.isPending ? 'Enviando…' : waiting ? `Espera ${wait.remaining} s` : 'Enviar código'}
        </button>
        <button type="button" className="button button--secondary" onClick={onCancel}>
          Cancelar
        </button>
      </div>
    </section>
  );
}
