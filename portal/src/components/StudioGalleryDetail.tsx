import type { ReactElement } from 'react';
import { useStudioGallery } from '../hooks/useStudioGallery';
import { useStudioLogout } from '../hooks/useStudioMutations';
import type { StudioGalleryDetailProps } from '../types/components/ProofComponents.types';
import { describeProofRequestError } from '../utils/proofMessages';
import { formatDateTime } from '../utils/format';
import { describeExtraPhoto, describeGalleryStatus, describePhotos } from '../utils/studio';
import { LoadingScreen } from './LoadingScreen';
import { StatusBadge } from './StatusBadge';
import { StudioProofs } from './StudioProofs';

/**
 * One gallery of the photographer, at `/studio/galleries/{id}`: who it is for and where it is, and its proofs.
 * @param props Component props.
 * @returns The page.
 */
export function StudioGalleryDetail({ galleryId, user }: StudioGalleryDetailProps): ReactElement {
  const gallery = useStudioGallery(galleryId);
  const logout = useStudioLogout();
  const back = (
    <a className="studio-back" href="/studio">
      ← Tus galerías
    </a>
  );

  if (gallery.isPending) {
    return <LoadingScreen message="Cargando la galería…" />;
  }

  if (gallery.isError) {
    return (
      <div className="stack">
        {back}
        <section className="card" role="alert">
          <h2>No pudimos abrir esta galería</h2>
          <p className="muted">{describeProofRequestError(gallery.error)}</p>
          <button type="button" className="button button--secondary" onClick={() => void gallery.refetch()}>
            Intentar de nuevo
          </button>
        </section>
      </div>
    );
  }

  const status = describeGalleryStatus(gallery.data.status);

  return (
    <div className="stack">
      <header className="studio-bar">
        {back}
        <div className="studio-bar__actions">
          <span className="muted">{user.name}</span>
          <button type="button" className="button button--secondary" disabled={logout.isPending} onClick={() => logout.mutate(false)}>
            Salir
          </button>
        </div>
      </header>
      <section className="card" aria-labelledby="gallery-title">
        <div className="gallery-card__head">
          <h1 id="gallery-title">{gallery.data.clientName}</h1>
          <StatusBadge label={status.label} tone={status.tone} />
        </div>
        <p>{gallery.data.packageName}</p>
        <p className="muted">{formatDateTime(gallery.data.sessionStart)}</p>
        <p>{describePhotos(gallery.data)}</p>
        <p className="muted">{describeExtraPhoto(gallery.data)}</p>
      </section>
      <StudioProofs gallery={gallery.data} />
    </div>
  );
}
