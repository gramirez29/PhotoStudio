import { useEffect, useState, type ReactElement } from 'react';
import { useGallery } from '../hooks/usePortalGallery';
import type { GalleryChooserProps } from '../types/components/GalleryComponents.types';
import { describeGalleryLoadError } from '../utils/apiErrors';
import { GalleryPicker } from './GalleryPicker';
import { GallerySentSummary } from './GallerySentSummary';
import { StepHeading } from './StepHeading';

/**
 * Third step of the gallery flow: loads the gallery and shows what fits where it is. While the selection is open the client
 * chooses; after they sent it they see what they chose. If the server refuses the session the client is sent back to confirm a
 * code, and the photos they had picked are kept.
 * @param props Component props.
 * @returns The step, or nothing while the client is sent back to the code.
 */
export function GalleryChooser({ token, session, onSessionExpired, onClose }: GalleryChooserProps): ReactElement | null {
  const gallery = useGallery(token, session.accessToken);
  const [justSent, setJustSent] = useState(false);
  const failure = gallery.isError ? describeGalleryLoadError(gallery.error) : null;
  const expired = failure?.sessionExpired === true;

  useEffect(() => {
    if (expired) {
      onSessionExpired();
    }
  }, [expired, onSessionExpired]);

  if (gallery.isPending) {
    return (
      <section className="card" aria-labelledby="proof-step-title">
        <StepHeading>Tus fotos</StepHeading>
        <p role="status">Cargando tus fotos…</p>
      </section>
    );
  }

  // The verification expired: the flow is already taking the client back to the code, so there is nothing to show here.
  if (expired) {
    return null;
  }

  if (!gallery.isSuccess) {
    return (
      <section className="card" aria-labelledby="proof-step-title">
        <StepHeading>Tus fotos</StepHeading>
        <p className="field__error" role="alert">
          {failure?.message}
        </p>
        <div className="actions">
          <button type="button" className="button" onClick={() => void gallery.refetch()}>
            Reintentar
          </button>
          <button type="button" className="button button--secondary" onClick={onClose}>
            Cerrar
          </button>
        </div>
      </section>
    );
  }

  return gallery.data.status === 'InSelection' && gallery.data.selection === null ? (
    <GalleryPicker token={token} session={session} gallery={gallery.data} onSessionExpired={onSessionExpired} onSent={() => setJustSent(true)} onClose={onClose} />
  ) : (
    <GallerySentSummary gallery={gallery.data} justSent={justSent} onClose={onClose} />
  );
}
