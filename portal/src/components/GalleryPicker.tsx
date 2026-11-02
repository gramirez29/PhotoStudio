import { useEffect, useState, type ReactElement } from 'react';
import { useSubmitSelection } from '../hooks/usePortalGallery';
import type { GalleryPickerProps } from '../types/components/GalleryComponents.types';
import type { ActionErrorView } from '../types/utils/apiErrors.types';
import { describeSelectionError } from '../utils/apiErrors';
import { clearDraft, describeChoice, readDraft, saveDraft, summarizeChoice, toggleChoice } from '../utils/gallerySelection';
import { formatMoney } from '../utils/format';
import { GalleryLightbox } from './GalleryLightbox';
import { GalleryPhotoTile } from './GalleryPhotoTile';
import { StepHeading } from './StepHeading';

/**
 * The gallery while the selection is open: the legend about the quality of the proofs, the grid, the counter that goes down with
 * every photo, what the extra photos cost, and the review before sending. The choice in progress is remembered in the tab, so a
 * reload or a verification that expires does not lose it. Sending locks the selection, and the page says so before it happens.
 * @param props Component props.
 * @returns The picker, or the review before sending.
 */
export function GalleryPicker({ token, session, gallery, onSessionExpired, onSent, onClose }: GalleryPickerProps): ReactElement {
  const submit = useSubmitSelection(token);
  const [chosen, setChosen] = useState<string[]>(() =>
    readDraft(
      token,
      gallery.photos.map((photo) => photo.id),
    ),
  );
  const [open, setOpen] = useState<number | null>(null);
  const [reviewing, setReviewing] = useState(false);
  const [confirmingExtras, setConfirmingExtras] = useState(false);
  const [error, setError] = useState<ActionErrorView | null>(null);

  useEffect(() => {
    saveDraft(token, chosen);
  }, [token, chosen]);

  const summary = summarizeChoice(chosen.length, gallery);
  const words = describeChoice(summary, gallery.extraPhotoPrice);
  const price = formatMoney(gallery.extraPhotoPrice);

  /**
   * Picks or drops a photo.
   * @param photoId Photo the client tapped.
   */
  function toggle(photoId: string): void {
    setError(null);
    setChosen((current) => toggleChoice(current, photoId));
  }

  /** Asks to send. When there are photos beyond the package the client must first accept that the studio will contact them about the payment. */
  function requestSend(): void {
    if (summary.extraPhotos > 0) {
      setError(null);
      setConfirmingExtras(true);
    } else {
      send(false);
    }
  }

  /** The client declined the extra photos: they are left out, the last ones chosen first, and the review shows what remains. */
  function omitExtras(): void {
    const kept = chosen.slice(0, gallery.selectablePhotos);
    setChosen(kept);
    setConfirmingExtras(false);
    setReviewing(kept.length > 0);
  }

  /**
   * Sends the selection. A verification that expired takes the client back to the code, with the choice kept.
   * @param acceptExtras True when the client accepted that the studio contacts them to agree on the payment of the extra photos.
   */
  function send(acceptExtras: boolean): void {
    setError(null);
    submit.mutate(
      { accessToken: session.accessToken, photoIds: chosen, acceptExtras },
      {
        onSuccess: () => {
          clearDraft(token);
          onSent();
        },
        onError: (failure) => {
          const view = describeSelectionError(failure);
          if (view.sessionExpired) {
            onSessionExpired();
          } else {
            setError(view);
          }
        },
      },
    );
  }

  if (reviewing && confirmingExtras) {
    return (
      <section className="card" aria-labelledby="gallery-extras-title">
        <StepHeading>Fotos adicionales</StepHeading>
        <p>
          Seleccionaste <strong>{summary.extraPhotos}</strong> {summary.extraPhotos === 1 ? 'foto adicional' : 'fotos adicionales'} ({formatMoney(summary.extraTotal)}).
          PhotoSense Studio va a contactarte para coordinar el pago.
        </p>
        <p>Si aceptas, las enviamos junto con las fotos de tu paquete. Si cancelas, se omiten las adicionales (las últimas que elegiste).</p>
        {error !== null && (
          <p className="field__error" role="alert">
            {error.message}
          </p>
        )}
        <div className="actions">
          <button type="button" className="button" disabled={submit.isPending} onClick={() => send(true)}>
            {submit.isPending ? 'Enviando…' : 'Aceptar y enviar'}
          </button>
          <button type="button" className="button button--secondary" disabled={submit.isPending} onClick={omitExtras}>
            Cancelar y omitir las adicionales
          </button>
        </div>
      </section>
    );
  }

  if (reviewing) {
    return (
      <section className="card" aria-labelledby="gallery-review-title">
        <StepHeading>Revisa tu selección</StepHeading>
        <p>
          Vas a enviar <strong>{chosen.length}</strong> {chosen.length === 1 ? 'foto' : 'fotos'}.
        </p>
        {words.extra !== null && <p>{words.extra}</p>}
        <p>Al enviar, le avisamos a tu fotógrafo cuáles fotos va a editar. Después de enviar no podrás cambiar tu selección.</p>
        {error !== null && (
          <p className="field__error" role="alert">
            {error.message}
          </p>
        )}
        <div className="actions">
          <button type="button" className="button" disabled={submit.isPending} onClick={requestSend}>
            {submit.isPending ? 'Enviando…' : 'Sí, enviar mi selección'}
          </button>
          <button type="button" className="button button--secondary" disabled={submit.isPending} onClick={() => setReviewing(false)}>
            Seguir eligiendo
          </button>
        </div>
      </section>
    );
  }

  return (
    <section className="card gallery" aria-labelledby="gallery-title">
      <StepHeading>Tus fotos de prueba</StepHeading>
      <p className="notice">
        Estás viendo pruebas en calidad baja y con marca de agua. Nuestro compromiso es entregarte fotografías de excelente calidad.
      </p>

      <div className="stack stack--tight gallery__counter" role="status" aria-live="polite">
        <p>
          <strong>{words.headline}</strong>
        </p>
        {words.extra !== null && <p>{words.extra}</p>}
        {words.invitation !== null && <p className="muted">{words.invitation}</p>}
        {words.invitation === null && <p className="muted">Si eliges más, cada foto adicional cuesta {price}.</p>}
      </div>

      <ul className="gallery__grid" aria-label="Fotos de prueba">
        {gallery.photos.map((photo, index) => (
          <GalleryPhotoTile
            key={photo.id}
            proof={photo}
            number={index + 1}
            chosen={chosen.includes(photo.id)}
            readOnly={false}
            onToggle={toggle}
            onEnlarge={(photoId) => setOpen(gallery.photos.findIndex((candidate) => candidate.id === photoId))}
          />
        ))}
      </ul>

      <div className="gallery__bar">
        <p aria-hidden="true">
          <strong>{chosen.length}</strong> {chosen.length === 1 ? 'elegida' : 'elegidas'}
        </p>
        <button type="button" className="button" disabled={chosen.length === 0} onClick={() => setReviewing(true)}>
          Revisar y enviar
        </button>
        <button type="button" className="button button--secondary" onClick={onClose}>
          Cerrar
        </button>
      </div>

      {open !== null && (
        <GalleryLightbox
          proofs={gallery.photos}
          index={open}
          chosen={chosen}
          readOnly={false}
          onToggle={toggle}
          onMove={setOpen}
          onClose={() => setOpen(null)}
        />
      )}
    </section>
  );
}
