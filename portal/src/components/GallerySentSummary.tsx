import { useState, type ReactElement } from 'react';
import type { GallerySentSummaryProps } from '../types/components/GalleryComponents.types';
import { formatMoney } from '../utils/format';
import { GalleryLightbox } from './GalleryLightbox';
import { GalleryPhotoTile } from './GalleryPhotoTile';
import { StepHeading } from './StepHeading';

/**
 * The gallery once the selection was sent: what was chosen, what the extra photos cost, and the photos themselves, which can only
 * be looked at. Right after sending it is worded as news; later it is a reminder.
 * @param props Component props.
 * @returns The summary.
 */
export function GallerySentSummary({ gallery, justSent, onClose }: GallerySentSummaryProps): ReactElement {
  const [open, setOpen] = useState<number | null>(null);
  const selection = gallery.selection;
  const chosenIds = selection?.photoIds ?? [];
  const chosenProofs = gallery.photos.filter((photo) => chosenIds.includes(photo.id));
  const extra = selection?.extraPhotos ?? 0;

  return (
    <section className="card gallery" aria-labelledby="gallery-sent-title">
      <StepHeading>{justSent ? '¡Listo! Recibimos tu selección' : 'Tu selección'}</StepHeading>
      <p role="status">
        {justSent
          ? 'Tu fotógrafo ya sabe cuáles fotos va a editar. Te avisaremos cuando estén listas.'
          : 'Tu fotógrafo está trabajando con las fotos que elegiste.'}
      </p>
      <p>
        <strong>{chosenIds.length}</strong> {chosenIds.length === 1 ? 'foto elegida' : 'fotos elegidas'}.
      </p>
      {selection !== null && extra > 0 && (
        <p>
          {extra} {extra === 1 ? 'foto adicional' : 'fotos adicionales'}: {formatMoney(selection.extraTotal)}. Se suma a tu saldo.
        </p>
      )}

      <ul className="gallery__grid" aria-label="Fotos que elegiste">
        {chosenProofs.map((photo, index) => (
          <GalleryPhotoTile key={photo.id} proof={photo} number={index + 1} chosen readOnly onToggle={() => undefined} onEnlarge={() => setOpen(index)} />
        ))}
      </ul>

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
