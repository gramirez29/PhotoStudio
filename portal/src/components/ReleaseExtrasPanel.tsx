import { useState, type ReactElement } from 'react';
import { useReleaseExtras } from '../hooks/useStudioGallery';
import type { ReleaseExtrasPanelProps } from '../types/components/ProofComponents.types';
import { describeFinalsRequestError } from '../utils/proofMessages';

/**
 * The step that lets the client download the extra photos. The system never charges them: the photographer agrees on the payment
 * with the client and releases them by hand, so it asks to confirm first.
 * @param props Component props.
 * @returns The panel.
 */
export function ReleaseExtrasPanel({ gallery, disabled }: ReleaseExtrasPanelProps): ReactElement {
  const [confirming, setConfirming] = useState(false);
  const release = useReleaseExtras(gallery.id);

  return (
    <section className="stack proof-publish" aria-labelledby="extras-release-title">
      <h3 id="extras-release-title">Entregar las fotos adicionales</h3>
      <p className="muted">
        Hazlo cuando el cliente ya pagó las adicionales. Verá las que tengan su final en la misma galería. Las que aún no tengan final no
        aparecerán.
      </p>
      {release.isError && (
        <p className="field__error" role="alert">
          {describeFinalsRequestError(release.error)}
        </p>
      )}
      {confirming ? (
        <div className="actions">
          <button type="button" className="button" disabled={release.isPending} onClick={() => release.mutate()}>
            {release.isPending ? 'Entregando…' : 'Sí, el cliente ya pagó'}
          </button>
          <button type="button" className="button button--secondary" disabled={release.isPending} onClick={() => setConfirming(false)}>
            Todavía no
          </button>
        </div>
      ) : (
        <button type="button" className="button" disabled={disabled} onClick={() => setConfirming(true)}>
          Entregar las adicionales
        </button>
      )}
    </section>
  );
}
