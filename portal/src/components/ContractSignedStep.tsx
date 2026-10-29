import type { ReactElement } from 'react';
import type { ContractSignedStepProps } from '../types/components/ContractSteps.types';
import { StepHeading } from './StepHeading';

/**
 * Last step of the contract flow: the signature was registered. The booking above already shows the contract as signed.
 * @param props Component props.
 * @returns The step.
 */
export function ContractSignedStep({ onDone }: ContractSignedStepProps): ReactElement {
  return (
    <section className="card" aria-labelledby="proof-step-title">
      <StepHeading>Firmaste tu contrato</StepHeading>
      <p>Guardamos tu firma junto con el texto exacto que leíste. Si falta el anticipo, tu reserva se confirma cuando tu fotógrafo lo verifique.</p>
      <div className="actions">
        <button type="button" className="button" onClick={onDone}>
          Listo
        </button>
      </div>
    </section>
  );
}
