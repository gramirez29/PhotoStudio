import type { ReactElement } from 'react';
import { useProofUpload } from '../hooks/useProofUpload';
import type { StudioProofsProps } from '../types/components/ProofComponents.types';
import { fileKey } from '../utils/proofSelection';
import { OpenSelectionPanel } from './OpenSelectionPanel';
import { ProofGrid } from './ProofGrid';
import { ProofPicker } from './ProofPicker';
import { ProofQueue } from './ProofQueue';
import { ProofSamplePreview } from './ProofSamplePreview';
import { StudioSelection } from './StudioSelection';

/**
 * The proofs of a gallery: what is already up, the way to add more (choose, see a sample, upload) while the API allows it, and the
 * step that publishes them. It shows what the API says the photographer can do (`allowedActions`) and never decides it by itself.
 * @param props Component props.
 * @returns The section.
 */
export function StudioProofs({ gallery }: StudioProofsProps): ReactElement {
  const upload = useProofUpload(gallery);
  const canUpload = gallery.allowedActions.includes('UploadProofs');
  const canRemove = gallery.allowedActions.includes('RemoveProof');
  const canPublish = gallery.allowedActions.includes('OpenSelection');
  const busy = upload.running || upload.items.length > 0;
  const room = gallery.maxPhotos - gallery.photoCount - upload.items.length;
  const published = gallery.status !== 'Pending';
  const selectionSent = gallery.selectionSubmittedAt !== null;

  return (
    <section className="card" aria-labelledby="proofs-title">
      <h2 id="proofs-title">Pruebas para el cliente</h2>
      <p className="muted">
        Se preparan aquí, en tu computadora: se reducen, se les baja la calidad y se les pone la marca de agua de PhotoSense Studio en el
        centro. Las fotos originales no se suben.
      </p>
      <p>
        <strong>
          {gallery.photoCount} de {gallery.maxPhotos} pruebas
        </strong>
      </p>
      {published && !selectionSent && (
        <p role="status">
          Las pruebas ya están publicadas. Si la reserva tiene correo, el cliente ya recibió el aviso; si no, avísale tú por WhatsApp.
        </p>
      )}
      {selectionSent && <StudioSelection gallery={gallery} />}
      {canUpload && upload.pending.length === 0 && <ProofPicker room={room} disabled={upload.running} onChoose={upload.choose} />}
      {upload.pending.length > 0 && <ProofSamplePreview key={fileKey(upload.pending[0] as File)} files={upload.pending} onConfirm={upload.confirm} onCancel={upload.cancel} />}
      {upload.notice !== null && <p className="field__hint">{upload.notice}</p>}
      <ProofQueue items={upload.items} running={upload.running} problem={upload.problem} onRetry={upload.retry} onDismiss={upload.dismissFailed} />
      <ProofGrid gallery={gallery} canRemove={canRemove && !upload.running} />
      {canPublish && <OpenSelectionPanel gallery={gallery} disabled={busy} />}
    </section>
  );
}
