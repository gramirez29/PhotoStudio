import { useReducer, useState, type ReactElement } from 'react';
import { initialProofValues } from '../forms/proofForm';
import type { ProofFlowProps } from '../types/components/ProofFlow.types';
import type { ProofFormValues } from '../types/forms/proofForm.types';
import { canStartProof } from '../utils/booking';
import { INITIAL_PROOF_FLOW, proofFlowReducer } from '../utils/proofFlow';
import { CodeEntryStep } from './CodeEntryStep';
import { CodeRequestStep } from './CodeRequestStep';
import { ProofFormStep } from './ProofFormStep';
import { ProofSentStep } from './ProofSentStep';

/** Message shown when the verification expires while the client fills the form. */
const SESSION_EXPIRED_NOTICE = 'Tu verificación venció. Confirma tu correo otra vez; lo que escribiste se conserva.';

/**
 * The payment proof flow of a booking: start, confirm the email with a code, fill in the transfer, done. It shows nothing the
 * API does not allow: with no email on the booking it says the photographer must add it, and once the whole balance is in review
 * there is nothing left to report.
 * @param props Component props.
 * @returns The current step, or nothing.
 */
export function ProofFlow({ token, booking, session, onSessionChange }: ProofFlowProps): ReactElement | null {
  const [state, dispatch] = useReducer(proofFlowReducer, INITIAL_PROOF_FLOW);
  const [values, setValues] = useState<ProofFormValues>(() => initialProofValues(booking));
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID());
  const [notice, setNotice] = useState<string | null>(null);

  /** Closes the flow and prepares the next proof: new suggested amount from the updated booking and a new idempotency key. */
  function finish(): void {
    dispatch({ type: 'close' });
    setValues(initialProofValues(booking));
    setIdempotencyKey(crypto.randomUUID());
  }

  switch (state.step) {
    case 'request':
      return (
        <CodeRequestStep
          token={token}
          purpose="subir tu comprobante"
          notice={notice}
          onSent={(maskedEmail) => {
            setNotice(null);
            dispatch({ type: 'codeSent', maskedEmail });
          }}
          onCancel={() => dispatch({ type: 'close' })}
        />
      );
    case 'enter':
      return (
        <CodeEntryStep
          token={token}
          maskedEmail={state.maskedEmail ?? ''}
          onVerified={(verified) => {
            onSessionChange(verified);
            dispatch({ type: 'verified', session: verified });
          }}
          onCancel={() => dispatch({ type: 'close' })}
        />
      );
    case 'form':
      return state.session === null ? null : (
        <ProofFormStep
          token={token}
          booking={booking}
          session={state.session}
          values={values}
          idempotencyKey={idempotencyKey}
          onChange={setValues}
          onSent={() => dispatch({ type: 'proofSent' })}
          onSessionExpired={() => {
            setNotice(SESSION_EXPIRED_NOTICE);
            onSessionChange(null);
            dispatch({ type: 'sessionExpired' });
          }}
          onCancel={() => dispatch({ type: 'close' })}
        />
      );
    case 'sent':
      return <ProofSentStep onDone={finish} />;
    case 'closed':
      break;
  }

  if (booking.canSubmitProof && !booking.canReceiveCode) {
    return (
      <section className="card" aria-labelledby="proof-title">
        <h2 id="proof-title">Comprobante de pago</h2>
        <p>Para subir tu comprobante, tu fotógrafo necesita registrar tu correo en la reserva. Escríbele y pídeselo.</p>
      </section>
    );
  }

  if (!canStartProof(booking)) {
    return null;
  }

  return (
    <section className="card" aria-labelledby="proof-title">
      <h2 id="proof-title">¿Ya pagaste por SINPE Móvil?</h2>
      <p>Cuéntale a tu fotógrafo cuánto transferiste para que lo verifique.</p>
      <div className="actions">
        <button type="button" className="button" onClick={() => dispatch({ type: 'open', now: Date.now(), session })}>
          Subir comprobante
        </button>
      </div>
    </section>
  );
}
