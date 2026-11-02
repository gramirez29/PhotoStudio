import { useState, type ReactElement } from 'react';
import type { PortalProof } from '../types/api/portal';
import type { GallerySentSummaryProps } from '../types/components/GalleryComponents.types';
import { formatMoney } from '../utils/format';
import { splitChoice } from '../utils/gallerySelection';
import { GalleryLightbox } from './GalleryLightbox';
import { GalleryPhotoTile } from './GalleryPhotoTile';
import { StepHeading } from './StepHeading';

/**
 * The gallery once the selection was sent: what was chosen, what the extra photos cost, and the photos themselves, which can only
 * be looked at. The photos of the package and the extra ones are shown apart, so the client sees what the package covers and what
 * is agreed on with the studio. Right after sending it is worded as news; later it is a reminder.
 * @param props Component props.
 * @returns The summary.
 */
export function GallerySentSummary({ gallery, justSent, onClose }: GallerySentSummaryProps): ReactElement {
  const [open, setOpen] = useState<number | null>(null);
  const selection = gallery.selection;
  const chosenIds = selection?.photoIds ?? [];
  const { packageIds, extraIds } = splitChoice(chosenIds, gallery.selectablePhotos);
  const proofOf = (id: string) => gallery.photos.find((photo) => photo.id === id);
  const packageProofs = packageIds.flatMap((id) => proofOf(id) ?? []);
  const extraProofs = extraIds.flatMap((id) => proofOf(id) ?? []);
  const chosenProofs = [...packageProofs, ...extraProofs];
  const extra = selection?.extraPhotos ?? 0;

  return (
    <section className="card gallery" aria-labelledby="gallery-sent-title">
      <StepHeading>{justSent ? '¡Listo! Recibimos tu selección' : 'Tu selección'}</StepHeading>
      <p role="status">
        {justSent
          ? 'Tu fotógrafo ya sabe cuáles fotos va a editar. Te avisaremos cuando estén listas.'
          : gallery.status === 'Ready'
            ? 'Tu fotógrafo terminó de editar. Podrás descargar tus fotos en cuanto tu saldo esté en cero.'
            : 'Tu fotógrafo está trabajando con las fotos que elegiste.'}
      </p>
      <p>
        <strong>{chosenIds.length}</strong> {chosenIds.length === 1 ? 'foto elegida' : 'fotos elegidas'}.
      </p>

      <ChosenGroup title="Fotos de tu paquete" proofs={packageProofs} offset={0} onEnlarge={setOpen} />
      {extra > 0 && (
        <>
          <p>
            {extra} {extra === 1 ? 'foto adicional' : 'fotos adicionales'}: {selection === null ? '' : formatMoney(selection.extraTotal)}. PhotoSense Studio te contactará para coordinar el pago.
          </p>
          <ChosenGroup title="Fotos adicionales" proofs={extraProofs} offset={packageProofs.length} onEnlarge={setOpen} />
        </>
      )}

      <div className="actions">
        <button type="button" className="button" onClick={onClose}>
          Cerrar
        </button>
      </div>

      {open !== null && (
        <GalleryLightbox proofs={chosenProofs} index={open} chosen={chosenIds} readOnly onToggle={() => undefined} onMove={setOpen} onClose={() => setOpen(null)} />
      )}
    </section>
  );
}

/** Props of one group of chosen photos. */
interface ChosenGroupProps {
  /** Heading of the group. */
  readonly title: string;
  /** The proofs of the group. */
  readonly proofs: readonly PortalProof[];
  /** How many photos come before this group, so the numbers and the large view follow on from the previous group. */
  readonly offset: number;
  /** Called with the position, among all the chosen photos, of the one the client wants to see larger. */
  readonly onEnlarge: (index: number) => void;
}

/**
 * One group of the chosen photos, with its own heading and grid.
 * @param props Component props.
 * @returns The group, or nothing when it has no photos.
 */
function ChosenGroup({ title, proofs, offset, onEnlarge }: ChosenGroupProps): ReactElement | null {
  if (proofs.length === 0) {
    return null;
  }

  return (
    <div className="stack stack--tight" role="group" aria-label={title}>
      <h3>
        {title} ({proofs.length})
      </h3>
      <ul className="gallery__grid" aria-label={title}>
        {proofs.map((photo, index) => (
          <GalleryPhotoTile key={photo.id} proof={photo} number={offset + index + 1} chosen readOnly onToggle={() => undefined} onEnlarge={() => onEnlarge(offset + index)} />
        ))}
      </ul>
    </div>
  );
}
