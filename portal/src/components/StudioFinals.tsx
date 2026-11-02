import type { ReactElement } from 'react';
import { useFinalUpload } from '../hooks/useFinalUpload';
import { useStudioSelection } from '../hooks/useStudioGallery';
import type { StudioFinalsProps } from '../types/components/ProofComponents.types';
import { formatDateTime } from '../utils/format';
import { describeProofRequestError } from '../utils/proofMessages';
import { FinalsPicker } from './FinalsPicker';
import { FinalsStaged } from './FinalsStaged';
import { ProofQueue } from './ProofQueue';
import { PublishFinalsPanel } from './PublishFinalsPanel';
import { ReleaseExtrasPanel } from './ReleaseExtrasPanel';

/**
 * The final photos of a gallery: how many of the package are up, the way to add more (choose files that are paired with the photos
 * the client chose by name) and the steps that publish the package and release the extras. It shows what the API says the
 * photographer can do (`allowedActions`) and never decides it by itself.
 * @param props Component props.
 * @returns The section.
 */
export function StudioFinals({ gallery }: StudioFinalsProps): ReactElement {
  const selection = useStudioSelection(gallery.id, true);
  const photos = selection.data?.photos ?? [];
  const upload = useFinalUpload(gallery, photos);
  const canUpload = gallery.allowedActions.includes('UploadFinals');
  const canPublish = gallery.allowedActions.includes('PublishFinals');
  const canRelease = gallery.allowedActions.includes('ReleaseExtras');
  const busy = upload.running || upload.items.length > 0 || upload.staged.length > 0;
  const packagePhotos = photos.filter((photo) => !photo.isExtra);
  const packageDone = packagePhotos.filter((photo) => photo.finalBytes !== null).length;

  if (selection.isError) {
    return (
      <p className="field__error" role="alert">
        {describeProofRequestError(selection.error)}
      </p>
    );
  }

  return (
    <section className="card" aria-labelledby="finals-title">
      <h2 id="finals-title">Fotos finales</h2>
      {selection.isPending ? (
        <p className="muted">Cargando…</p>
      ) : (
        <p>
          <strong>
            {packageDone} de {packagePhotos.length} del paquete con su final
          </strong>
        </p>
      )}
      {gallery.deliveredAt !== null && (
        <p role="status">Entregada el {formatDateTime(gallery.deliveredAt)}: el cliente ya puede descargar sus fotos.</p>
      )}
      {gallery.deliveredAt === null && gallery.finalsPublishedAt !== null && (
        <p role="status">
          Publicadas el {formatDateTime(gallery.finalsPublishedAt)}. El cliente las descargará en cuanto su saldo esté en cero.
        </p>
      )}
      {gallery.extrasReleasedAt !== null && <p role="status">Las fotos adicionales ya están disponibles para el cliente.</p>}
      {canUpload && upload.staged.length === 0 && <FinalsPicker disabled={upload.running} onChoose={upload.choose} />}
      {upload.staged.length > 0 && <FinalsStaged staged={upload.staged} onConfirm={upload.confirm} onCancel={upload.cancel} />}
      {upload.notice !== null && <p className="field__hint">{upload.notice}</p>}
      <ProofQueue items={upload.items} running={upload.running} problem={upload.problem} onRetry={upload.retry} onDismiss={upload.dismissFailed} />
      {canPublish && <PublishFinalsPanel gallery={gallery} disabled={busy} />}
      {canRelease && <ReleaseExtrasPanel gallery={gallery} disabled={busy} />}
    </section>
  );
}
