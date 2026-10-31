import { useState, type ReactElement } from 'react';
import { useOpenSelection } from '../hooks/useStudioGallery';
import type { OpenSelectionPanelProps } from '../types/components/ProofComponents.types';
import { describeProofRequestError } from '../utils/proofMessages';

/**
 * The step that publishes the proofs: the client is told by email and can start choosing. It asks to confirm first, because after
 * this the proofs can no longer be changed.
 * @param props Component props.
 * @returns The panel.
 */
export function OpenSelectionPanel({ gallery, disabled }: OpenSelectionPanelProps): ReactElement {
  const [confirming, setConfirming] = useState(false);
  const open = useOpenSelection(gallery.id);
  const count = gallery.photoCount;
  const noun = count === 1 ? 'prueba' : 'pruebas';

  return (
    <section className="stack proof-publish" aria-labelledby="proof-publish-title">
      <h3 id="proof-publish-title">Publicar las pruebas</h3>
      <p className="muted">
        El cliente recibe un correo y elige sus fotos desde el enlace de su reserva, el mismo que le mandaste por WhatsApp. Después de
        publicar ya no podrás cambiar las pruebas.
      </p>
      {open.isError && (
        <p className="field__error" role="alert">
          {describeProofRequestError(open.error)}
        </p>
      )}
      {confirming ? (
        <div className="actions">
          <button type="button" className="button" disabled={open.isPending} onClick={() => open.mutate()}>
            {open.isPending ? 'Publicando…' : `Sí, publicar ${count} ${noun}`}
          </button>
          <button type="button" className="button button--secondary" disabled={open.isPending} onClick={() => setConfirming(false)}>
            Todavía no
          </button>
        </div>
      ) : (
        <button type="button" className="button" disabled={disabled || count === 0} onClick={() => setConfirming(true)}>
          Publicar pruebas
        </button>
      )}
      {count === 0 && <p className="field__hint">Sube al menos una prueba para poder publicarlas.</p>}
      {disabled && count > 0 && <p className="field__hint">Espera a que terminen de subir las fotos.</p>}
    </section>
  );
}
