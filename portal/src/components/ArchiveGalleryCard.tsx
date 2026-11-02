import { useState, type ReactElement } from 'react';
import { useFolderDownload } from '../hooks/useFolderDownload';
import type { ArchiveGalleryCardProps } from '../types/components/StudioComponents.types';
import { formatDateTime } from '../utils/format';
import { ReleaseGalleryPanel } from './ReleaseGalleryPanel';

/**
 * One delivered gallery of the page of the galleries to archive: who it is for, when it was delivered, the way to save its final
 * photos on this computer and, once they are saved (or the photographer says they already have them), the way to free the space.
 * Where the browser can write into a folder, the photos are received, checked against the size recorded and written there; elsewhere
 * the downloads start one by one and cannot be checked. Freeing the space is never offered before one of the two.
 * @param props Component props.
 * @returns The card.
 */
export function ArchiveGalleryCard({ gallery }: ArchiveGalleryCardProps): ReactElement {
  const download = useFolderDownload(gallery);
  const [ownBackup, setOwnBackup] = useState(false);
  const running = download.phase === 'running';
  const verified = download.phase === 'done';
  const canRelease = verified || download.phase === 'started' || ownBackup;

  return (
    <section className="card stack stack--tight" aria-label={`Galería de ${gallery.clientName}`}>
      <h2>{gallery.clientName}</h2>
      <p className="muted">
        {gallery.packageName}
        {gallery.deliveredAt === null ? '' : ` · entregada el ${formatDateTime(gallery.deliveredAt)}`}
      </p>
      <div className="actions">
        <button type="button" className="button" disabled={running} onClick={download.start}>
          {running ? 'Descargando…' : download.canUseFolder ? 'Descargar a una carpeta' : 'Descargar los finales'}
        </button>
        <a className="button button--secondary" href={`/studio/galleries/${gallery.id}`}>
          Abrir la galería
        </a>
      </div>
      {!download.canUseFolder && <p className="field__hint">Este navegador baja los archivos uno por uno y no puede comprobar que quedaron completos. En Chrome o Edge puedes elegir una carpeta y se comprueba cada foto.</p>}
      {running && (
        <p role="status" aria-live="polite">
          Descargando {Math.min(download.handled + 1, download.total)} de {download.total}… no cierres esta página.
        </p>
      )}
      {verified && (
        <p role="status">
          {download.total === 0 ? 'Esta galería no tiene finales.' : `Listo: ${download.saved} ${download.saved === 1 ? 'foto guardada' : 'fotos guardadas'} y comprobadas.`}
        </p>
      )}
      {download.phase === 'started' && <p role="status">Se iniciaron {download.saved} descargas. Revisa tu carpeta de descargas antes de liberar el espacio.</p>}
      {download.problem !== null && (
        <p className="field__error" role="alert">
          {download.problem}
        </p>
      )}
      {download.failed.length > 0 && (
        <div className="field__error" role="alert">
          <p>No se pudieron guardar {download.failed.length} {download.failed.length === 1 ? 'foto' : 'fotos'}: vuelve a intentarlo antes de liberar.</p>
          <ul>
            {download.failed.map((failure) => (
              <li key={failure.photoId}>
                {failure.name} ({failure.reason === 'size' ? 'llegó con otro tamaño' : 'no se pudo recibir o guardar'})
              </li>
            ))}
          </ul>
        </div>
      )}
      {!canRelease && (
        <label className="checkbox">
          <input type="checkbox" checked={ownBackup} onChange={(event) => setOwnBackup(event.target.checked)} />
          <span>Ya tengo estas fotos respaldadas en mi computadora</span>
        </label>
      )}
      {canRelease && <ReleaseGalleryPanel gallery={gallery} disabled={running} />}
    </section>
  );
}
