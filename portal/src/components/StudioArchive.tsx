import type { ReactElement } from 'react';
import { useArchivableGalleries } from '../hooks/useArchivableGalleries';
import { describeLoadError } from '../utils/apiErrors';
import { ArchiveGalleryCard } from './ArchiveGalleryCard';
import { LoadErrorScreen } from './LoadErrorScreen';
import { LoadingScreen } from './LoadingScreen';

/**
 * The galleries to archive, at `/studio/archive`: the delivered ones, the one delivered longest ago first. For each, the photographer
 * can save its final photos on this computer and then free the space in the storage. The system also tells them once, 90 days after the
 * delivery, and archives by itself what is left after 180 days.
 * @returns The page.
 */
export function StudioArchive(): ReactElement {
  const galleries = useArchivableGalleries();

  let content: ReactElement;
  if (galleries.isPending) {
    content = <LoadingScreen message="Cargando las galerías…" />;
  } else if (galleries.isError) {
    content = <LoadErrorScreen view={describeLoadError(galleries.error)} onRetry={() => void galleries.refetch()} />;
  } else if (galleries.data.length === 0) {
    content = (
      <section className="card">
        <h2>No hay galerías por archivar</h2>
        <p className="muted">Aquí aparecen las galerías que ya entregaste.</p>
      </section>
    );
  } else {
    content = (
      <div className="stack">
        {galleries.data.map((gallery) => (
          <ArchiveGalleryCard key={gallery.id} gallery={gallery} />
        ))}
      </div>
    );
  }

  return (
    <div className="stack">
      <header className="studio-bar">
        <div className="stack stack--tight">
          <h1>Galerías por archivar</h1>
          <p className="muted">Guarda las fotos en tu computadora y después libera el espacio. Al liberar se borran de forma definitiva.</p>
        </div>
        <div className="studio-bar__actions">
          <a className="button button--secondary" href="/studio">
            Volver a mis galerías
          </a>
        </div>
      </header>
      {content}
    </div>
  );
}
