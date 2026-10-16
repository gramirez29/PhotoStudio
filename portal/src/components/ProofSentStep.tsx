import type { ReactElement } from 'react';
import type { ProofSentStepProps } from '../types/components/ProofSentStep.types';
import { StepHeading } from './StepHeading';

/**
 * Last step of the proof flow: the proof was registered and now waits for the photographer. The payments list above shows it
 * as "En revisión".
 * @param props Component props.
 * @returns The step.
 */
export function ProofSentStep({ onDone }: ProofSentStepProps): ReactElement {
  return (
    <section className="card" aria-labelledby="proof-step-title">
      <StepHeading>Recibimos tu comprobante</StepHeading>
      <p>Tu fotógrafo lo revisará y lo verificará pronto. Verás el estado en la lista de pagos de esta página.</p>
      <div className="actions">
        <button type="button" className="button" onClick={onDone}>
          Listo
        </button>
      </div>
    </section>
  );
}
