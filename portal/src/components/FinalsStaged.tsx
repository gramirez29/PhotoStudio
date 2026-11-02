import type { ReactElement } from 'react';
import type { FinalsStagedProps } from '../types/components/ProofComponents.types';
import { formatBytes } from '../utils/format';

/**
 * The files that were paired with a photo of the selection, shown before anything goes up so the photographer can check that each
 * final is going to the right photo.
 * @param props Component props.
 * @returns The list and its buttons.
 */
export function FinalsStaged({ staged, onConfirm, onCancel }: FinalsStagedProps): ReactElement {
  const total = staged.reduce((sum, match) => sum + match.file.size, 0);

  return (
    <section className="stack stack--tight" aria-label="Finales por subir">
      <p role="status">
        <strong>
          Se subirán {staged.length} {staged.length === 1 ? 'final' : 'finales'} ({formatBytes(total)}).
        </strong>
      </p>
      <ul className="proof-queue__list">
        {staged.map((match) => (
          <li key={match.photoId} className="proof-queue__item">
            <span className="proof-queue__name">{match.file.name}</span>
            <span className="proof-queue__state">→ {match.photoName}</span>
          </li>
        ))}
      </ul>
      <div className="actions">
        <button type="button" className="button" onClick={onConfirm}>
          Subir {staged.length} {staged.length === 1 ? 'final' : 'finales'}
        </button>
        <button type="button" className="button button--secondary" onClick={onCancel}>
          Cancelar
        </button>
      </div>
    </section>
  );
}
