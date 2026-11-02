import { useState, type ReactElement } from 'react';
import { usePublishFinals } from '../hooks/useStudioGallery';
import type { PublishFinalsPanelProps } from '../types/components/ProofComponents.types';
import { describeFinalsRequestError } from '../utils/proofMessages';

/**
 * The step that publishes the final photos of the package. It asks to confirm first, because after this the finals of the package
 * can no longer be changed. The client downloads them when the balance is zero, which the system checks by itself.
 * @param props Component props.
 * @returns The panel.
 */
export function PublishFinalsPanel({ gallery, disabled }: PublishFinalsPanelProps): ReactElement {
  const [confirming, setConfirming] = useState(false);
  const publish = usePublishFinals(gallery.id);

  return (
    <section className="stack proof-publish" aria-labelledby="finals-publish-title">
      <h3 id="finals-publish-title">Publicar los finales del paquete</h3>
      <p className="muted">
        Si el cliente no debe nada, puede descargarlas de inmediato y recibe un correo. Si debe saldo, las descargará cuando se verifique su
        pago. Después de publicar ya no podrás cambiar los finales del paquete.
      </p>
      {publish.isError && (
        <p className="field__error" role="alert">
          {describeFinalsRequestError(publish.error)}
        </p>
      )}
      {confirming ? (
        <div className="actions">
          <button type="button" className="button" disabled={publish.isPending} onClick={() => publish.mutate()}>
            {publish.isPending ? 'Publicando…' : 'Sí, publicar los finales'}
          </button>
          <button type="button" className="button button--secondary" disabled={publish.isPending} onClick={() => setConfirming(false)}>
            Todavía no
          </button>
        </div>
      ) : (
        <button type="button" className="button" disabled={disabled} onClick={() => setConfirming(true)}>
          Publicar finales
        </button>
      )}
      {disabled && <p className="field__hint">Espera a que terminen de subir los finales.</p>}
    </section>
  );
}
