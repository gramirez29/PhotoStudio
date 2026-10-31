import type { ReactElement } from 'react';
import { useRemoveProof, useStudioProofs } from '../hooks/useStudioGallery';
import type { ProofGridProps } from '../types/components/ProofComponents.types';
import { describeProofRequestError } from '../utils/proofMessages';

/**
 * The proofs the gallery has so far, with the watermark as the client will see them, and a way to remove each one while the
 * selection is not open. The addresses that show them are signed and last a few minutes; the list asks for new ones in time.
 * @param props Component props.
 * @returns The grid.
 */
export function ProofGrid({ gallery, canRemove }: ProofGridProps): ReactElement {
  const proofs = useStudioProofs(gallery.id);
  const remove = useRemoveProof(gallery.id);

  if (proofs.isPending) {
    return <p className="muted">Cargando las pruebas…</p>;
  }

  if (proofs.isError) {
    return (
      <div className="stack stack--tight">
        <p className="field__error" role="alert">
          {describeProofRequestError(proofs.error)}
        </p>
        <button type="button" className="button button--secondary" onClick={() => void proofs.refetch()}>
          Intentar de nuevo
        </button>
      </div>
    );
  }

  if (proofs.data.length === 0) {
    return <p className="muted">Todavía no hay pruebas.</p>;
  }

  return (
    <div className="stack stack--tight">
      {remove.isError && (
        <p className="field__error" role="alert">
          {describeProofRequestError(remove.error)}
        </p>
      )}
      <ul className="proof-grid">
        {proofs.data.map((proof) => (
          <li key={proof.id} className="proof-grid__item">
            <img src={proof.url} alt={proof.name} loading="lazy" decoding="async" referrerPolicy="no-referrer" />
            <span className="proof-grid__name">{proof.name}</span>
            {canRemove && (
              <button
                type="button"
                className="button button--secondary"
                aria-label={`Quitar ${proof.name}`}
                disabled={remove.isPending}
                onClick={() => remove.mutate(proof.id)}
              >
                Quitar
              </button>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
