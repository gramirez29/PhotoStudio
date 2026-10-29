import { useCallback, useReducer, useState, type ReactElement } from 'react';
import { INITIAL_CONTRACT_VALUES } from '../forms/contractForm';
import type { ContractFlowProps } from '../types/components/ContractSteps.types';
import type { ContractFormValues } from '../types/forms/contractForm.types';
import { contractFlowReducer, INITIAL_CONTRACT_FLOW } from '../utils/contractFlow';
import { CodeEntryStep } from './CodeEntryStep';
import { CodeRequestStep } from './CodeRequestStep';
import { ContractReadStep } from './ContractReadStep';
import { ContractSignedStep } from './ContractSignedStep';

/** Message shown when the verification expires while the client fills the form. */
const SESSION_EXPIRED_NOTICE = 'Tu verificación venció. Confirma tu correo otra vez; lo que escribiste se conserva.';

/**
 * The contract of a booking: its status and the way to read and sign it. The contract holds personal data, so reading it asks
 * for the code sent to the client's email, like any action that changes something. It offers only what the API allows: with no
 * email on the booking it says the photographer must add it.
 * @param props Component props.
 * @returns The current step, or the contract card.
 */
export function ContractFlow({ token, booking, session, onSessionChange }: ContractFlowProps): ReactElement | null {
  const [state, dispatch] = useReducer(contractFlowReducer, INITIAL_CONTRACT_FLOW);
  const [values, setValues] = useState<ContractFormValues>(INITIAL_CONTRACT_VALUES);
  const [notice, setNotice] = useState<string | null>(null);

  // A stable function: the read step depends on it in an effect that must not run again on every render.
  const expire = useCallback((): void => {
    setNotice(SESSION_EXPIRED_NOTICE);
    onSessionChange(null);
    dispatch({ type: 'sessionExpired' });
  }, [onSessionChange]);

  switch (state.step) {
    case 'request':
      return (
        <CodeRequestStep
          token={token}
          purpose="ver y firmar tu contrato"
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
    case 'read':
      return state.session === null ? null : (
        <ContractReadStep
          token={token}
          session={state.session}
          values={values}
          onChange={setValues}
          onSigned={() => dispatch({ type: 'signed' })}
          onSessionExpired={expire}
          onClose={() => dispatch({ type: 'close' })}
        />
      );
    case 'signed':
      return <ContractSignedStep onDone={() => dispatch({ type: 'close' })} />;
    case 'closed':
      break;
  }

  const needsEmail = booking.status === 'Tentative' && !booking.contractSigned && !booking.canReceiveCode;
  const canRead = booking.canReceiveCode && (booking.canSignContract || booking.contractSigned);

  return (
    <section className="card" aria-labelledby="contract-title">
      <h2 id="contract-title">Contrato</h2>
      <p>{booking.contractSigned ? 'Firmado' : 'Pendiente de firma'}</p>
      {needsEmail && <p>Tu fotógrafo debe registrar tu correo para que puedas firmarlo aquí. Escríbele y pídeselo.</p>}
      {canRead && (
        <div className="actions">
          <button type="button" className="button" onClick={() => dispatch({ type: 'open', now: Date.now(), session })}>
            {booking.contractSigned ? 'Ver contrato firmado' : 'Leer y firmar contrato'}
          </button>
        </div>
      )}
    </section>
  );
}
