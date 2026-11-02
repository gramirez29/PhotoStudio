import { useState, type ReactElement } from 'react';
import { useReleaseGallery } from '../hooks/useStudioGallery';
import type { ReleaseGalleryPanelProps } from '../types/components/ProofComponents.types';
import { describeFinalsRequestError } from '../utils/proofMessages';

/**
 * Frees the space of a delivered gallery: after confirming, every photo and the ZIP are deleted from the storage and the gallery is
 * archived. It cannot be undone, so it says so and asks first. The system does the same by itself 90 days after the delivery.
 * @param props Component props.
 * @returns The section.
 */
export function ReleaseGalleryPanel({ gallery, disabled }: ReleaseGalleryPanelProps): ReactElement {
  const release = useReleaseGallery(gallery.id);
  const [confirming, setConfirming] = useState(false);

  return (
    <section className="stack stack--tight" aria-labelledby="release-title">
      <h3 id="release-title">Liberar espacio</h3>
      <p className="muted">
        Cuando ya tengas las fotos en tu computadora, puedes borrarlas de aquí. Se borran las pruebas, los finales y el ZIP, y la galería
        queda archivada: el cliente ya no podrá descargar. Si no lo haces, se archiva sola 180 días después de la entrega (a los 90 te avisamos).
      </p>
      {release.isError && (
        <p className="field__error" role="alert">
          {describeFinalsRequestError(release.error)}
        </p>
      )}
      {!confirming ? (
        <div className="actions">
          <button type="button" className="button button--secondary" disabled={disabled || release.isPending} onClick={() => setConfirming(true)}>
            Liberar espacio
          </button>
        </div>
      ) : (
        <div className="stack stack--tight">
          <p>¿Ya descargaste las fotos? Esto no se puede deshacer: se borran de forma definitiva y el cliente ya no podrá descargarlas.</p>
          <div className="actions">
            <button type="button" className="button" disabled={release.isPending} onClick={() => release.mutate(undefined, { onSettled: () => setConfirming(false) })}>
              {release.isPending ? 'Borrando…' : 'Sí, borrar todo'}
            </button>
            <button type="button" className="button button--secondary" disabled={release.isPending} onClick={() => setConfirming(false)}>
              Cancelar
            </button>
          </div>
        </div>
      )}
    </section>
  );
}
