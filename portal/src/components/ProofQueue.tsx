import type { ReactElement } from 'react';
import type { ProofQueueProps } from '../types/components/ProofComponents.types';
import { describeUploadStatus } from '../utils/proofMessages';

/**
 * The photos that are not registered yet and where each one is: waiting, being prepared, going up, being confirmed, or failed with
 * the reason. While it runs it says how many are left; when it stops it offers to try again.
 * @param props Component props.
 * @returns The queue, or nothing when it is empty.
 */
export function ProofQueue({ items, running, problem, onRetry, onDismiss }: ProofQueueProps): ReactElement | null {
  if (items.length === 0) {
    return null;
  }

  const failed = items.filter((item) => item.status === 'failed').length;
  const summary = running
    ? `Subiendo… quedan ${items.length} ${items.length === 1 ? 'foto' : 'fotos'}. No cierres esta página.`
    : `${items.length} ${items.length === 1 ? 'foto no se subió' : 'fotos no se subieron'}.`;

  return (
    <section className="stack proof-queue" aria-label="Fotos por subir">
      <p role="status" aria-live="polite">
        <strong>{summary}</strong>
      </p>
      {problem !== null && (
        <p className="field__error" role="alert">
          {problem}
        </p>
      )}
      <ul className="proof-queue__list">
        {items.map((item) => (
          <li key={item.id} className="proof-queue__item">
            <span className="proof-queue__name">{item.file.name}</span>
            <span className={item.status === 'failed' ? 'proof-queue__state proof-queue__state--failed' : 'proof-queue__state'}>
              {item.problem ?? describeUploadStatus(item.status)}
            </span>
          </li>
        ))}
      </ul>
      {!running && (
        <div className="actions">
          <button type="button" className="button" onClick={onRetry}>
            Reintentar
          </button>
          {failed > 0 && (
            <button type="button" className="button button--secondary" onClick={onDismiss}>
              Quitar las que fallaron
            </button>
          )}
        </div>
      )}
    </section>
  );
}
