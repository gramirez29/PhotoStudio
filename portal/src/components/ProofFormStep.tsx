import { useState, type FormEvent, type ReactElement } from 'react';
import { validateProofForm } from '../forms/proofForm';
import { useSubmitProof } from '../hooks/useProofMutations';
import type { ProofFormProps } from '../types/components/ProofFormStep.types';
import type { ProofFormErrors } from '../types/forms/proofForm.types';
import { describeSubmitError } from '../utils/apiErrors';
import { reportableAmount } from '../utils/booking';
import { APP_LOCALE, formatMoney } from '../utils/format';
import { isSessionValid } from '../utils/proofFlow';
import { currencySymbol } from '../utils/price';
import { StepHeading } from './StepHeading';
import { TextField } from './TextField';

/**
 * Third step of the proof flow: the client says how much they transferred by SINPE Móvil and, if they have it, the reference.
 * What they typed lives in the parent, so it survives a verification that expires halfway. The idempotency key is created
 * once per proof, so a double tap or a retry after a lost answer registers it once.
 * @param props Component props.
 * @returns The step.
 */
export function ProofFormStep({
  token,
  booking,
  session,
  values,
  idempotencyKey,
  onChange,
  onSent,
  onSessionExpired,
  onCancel,
}: ProofFormProps): ReactElement {
  const submitProof = useSubmitProof(token);
  const [errors, setErrors] = useState<ProofFormErrors>({});
  const [failure, setFailure] = useState<string | null>(null);

  /**
   * Validates what was typed and registers the proof.
   * @param event Submit event of the form.
   */
  function submit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    setFailure(null);

    const result = validateProofForm(values, booking, idempotencyKey);
    if (!result.ok) {
      setErrors(result.errors);
      return;
    }

    setErrors({});
    if (!isSessionValid(session, Date.now())) {
      onSessionExpired();
      return;
    }

    submitProof.mutate(
      { accessToken: session.accessToken, request: result.request },
      {
        onSuccess: onSent,
        onError: (error) => {
          const view = describeSubmitError(error);
          if (view.sessionExpired) {
            onSessionExpired();
            return;
          }

          setFailure(view.message);
        },
      },
    );
  }

  const available = formatMoney({ amount: reportableAmount(booking), currency: booking.balance.currency });

  return (
    <section className="card" aria-labelledby="proof-step-title">
      <StepHeading>Tu comprobante de SINPE Móvil</StepHeading>
      <p className="muted">Puedes reportar hasta {available}, que es lo que falta por pagar.</p>

      <form className="stack" onSubmit={submit} noValidate>
        <TextField
          id="amount"
          label="Monto que transferiste"
          value={values.amount}
          onChange={(amount) => {
            onChange({ ...values, amount });
            setErrors({ ...errors, amount: undefined });
          }}
          error={errors.amount}
          inputMode="numeric"
          prefix={currencySymbol(booking.balance.currency, APP_LOCALE)}
          autoFocus
        />
        <TextField
          id="reference"
          label="Número de referencia (opcional)"
          value={values.reference}
          onChange={(reference) => {
            onChange({ ...values, reference });
            setErrors({ ...errors, reference: undefined });
          }}
          error={errors.reference}
          hint="Lo encuentras en el mensaje de confirmación de tu transferencia. Ayuda a tu fotógrafo a encontrarla."
          autoComplete="off"
        />

        {failure !== null && (
          <p className="field__error" role="alert">
            {failure}
          </p>
        )}

        <div className="actions">
          <button type="submit" className="button" disabled={submitProof.isPending}>
            {submitProof.isPending ? 'Enviando…' : 'Enviar comprobante'}
          </button>
          <button type="button" className="button button--secondary" onClick={onCancel}>
            Cancelar
          </button>
        </div>
      </form>
    </section>
  );
}
