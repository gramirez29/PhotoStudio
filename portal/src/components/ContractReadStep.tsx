import { useEffect, type ReactElement } from 'react';
import { useContract } from '../hooks/useContract';
import type { ContractReadStepProps } from '../types/components/ContractSteps.types';
import { describeContractLoadError } from '../utils/apiErrors';
import { ContractDocument } from './ContractDocument';
import { ContractSignForm } from './ContractSignForm';
import { StepHeading } from './StepHeading';

/**
 * Third step of the contract flow: the client reads the contract and, when it is unsigned, signs it right below. A contract that
 * is already signed shows the text that was signed, with no form. If the server refuses the session the client is sent back to
 * confirm a code, and what they typed is kept.
 * @param props Component props.
 * @returns The step.
 */
export function ContractReadStep({ token, session, values, onChange, onSigned, onSessionExpired, onClose }: ContractReadStepProps): ReactElement {
  const contract = useContract(token, session.accessToken);
  const failure = contract.isError ? describeContractLoadError(contract.error) : null;
  const expired = failure?.sessionExpired === true;

  useEffect(() => {
    if (expired) {
      onSessionExpired();
    }
  }, [expired, onSessionExpired]);

  return (
    <section className="card" aria-labelledby="proof-step-title">
      <StepHeading>Tu contrato</StepHeading>

      {contract.isPending && <p role="status">Cargando el contrato…</p>}

      {failure !== null && !expired && (
        <>
          <p className="field__error" role="alert">
            {failure.message}
          </p>
          <div className="actions">
            <button type="button" className="button" onClick={() => void contract.refetch()}>
              Reintentar
            </button>
            <button type="button" className="button button--secondary" onClick={onClose}>
              Cerrar
            </button>
          </div>
        </>
      )}

      {contract.isSuccess && (
        <div className="stack">
          <ContractDocument text={contract.data.text} />
          {contract.data.signed ? (
            <>
              <p className="muted">Este contrato ya está firmado.</p>
              <div className="actions">
                <button type="button" className="button" onClick={onClose}>
                  Cerrar
                </button>
              </div>
            </>
          ) : (
            <ContractSignForm
              token={token}
              session={session}
              templateVersion={contract.data.templateVersion}
              values={values}
              onChange={onChange}
              onSigned={onSigned}
              onSessionExpired={onSessionExpired}
              onCancel={onClose}
            />
          )}
        </div>
      )}
    </section>
  );
}
